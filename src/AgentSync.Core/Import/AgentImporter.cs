using AgentSync.Core.Adapters;
using AgentSync.Core.Configuration;
using AgentSync.Core.Projections;

namespace AgentSync.Core.Import;

/// <summary>Options for <see cref="AgentImporter"/> (mirrors the <c>agent import agent</c> flags).</summary>
public sealed record AgentImportOptions(
    string? Type = null,
    string Split = "file",
    string? Id = null,
    bool IncludeGenerated = false,
    bool Force = false,
    bool DryRun = false);

/// <summary>
/// Imports an existing tool-specific instruction file or folder (AGENTS.md, CLAUDE.md,
/// Copilot, Gemini, Cursor rules, or a skills root) into canonical skill(s). Generated
/// <c>agent-sync</c> sections are skipped by default; source files are only read, never
/// modified. Skill-shaped sources delegate to <see cref="SkillImporter"/>.
/// </summary>
public sealed class AgentImporter
{
    private static readonly string[] KnownTypes =
        { TargetIds.AgentsMd, TargetIds.ClaudeMd, TargetIds.Copilot, TargetIds.Gemini, TargetIds.Cursor };

    private readonly RepoLayout _layout;
    private readonly SkillImporter _skillImporter;

    public AgentImporter(string repoRoot)
    {
        _layout = new RepoLayout(repoRoot);
        _skillImporter = new SkillImporter(repoRoot);
    }

    public ImportReport Import(string rawPath, AgentImportOptions options)
    {
        if (options.Split is not ("file" or "sections"))
        {
            return ImportReport.Failure(ImportStatus.InvalidSource, $"Unknown --split '{options.Split}'. Use 'file' or 'sections'.", options.DryRun);
        }

        if (options.Type is not null && !KnownTypes.Contains(options.Type))
        {
            return ImportReport.Failure(ImportStatus.InvalidSource,
                $"Unknown --type '{options.Type}'. Known types: {string.Join(", ", KnownTypes)}.", options.DryRun);
        }

        ImportSource source;
        try
        {
            source = SourceDetector.Detect(_layout.RepoRoot, rawPath);
        }
        catch (RepoPathException ex)
        {
            return ImportReport.Failure(ImportStatus.UnsafePath, ex.Message, options.DryRun);
        }

        if (source.Kind == ImportSourceKind.Missing)
        {
            return ImportReport.Failure(ImportStatus.SourceNotFound, source.Reason!, options.DryRun);
        }

        var kind = ResolveKind(source, options.Type);

        return kind switch
        {
            ImportSourceKind.SkillFolder or ImportSourceKind.SkillFile =>
                _skillImporter.Import(rawPath, new SkillImportOptions(options.Id, Force: options.Force, DryRun: options.DryRun)),
            ImportSourceKind.SkillsRoot => ImportSkillsRoot(source, options),
            ImportSourceKind.CursorRulesDir => ImportCursorDir(source, options),
            ImportSourceKind.CursorRuleFile => ImportCursorFiles(new[] { source.AbsolutePath }, options),
            ImportSourceKind.AgentsMd or ImportSourceKind.ClaudeMd or ImportSourceKind.Copilot or ImportSourceKind.Gemini =>
                ImportSharedMarkdown(source, kind, options),
            _ => ImportReport.Failure(ImportStatus.InvalidSource,
                $"'{source.RelativePath}' is not a recognized instruction source. Use --type to choose one. ({source.Reason})",
                options.DryRun),
        };
    }

    private ImportSourceKind ResolveKind(ImportSource source, string? type)
    {
        if (type is null)
        {
            return source.Kind;
        }

        if (type == TargetIds.Cursor)
        {
            return Directory.Exists(source.AbsolutePath) ? ImportSourceKind.CursorRulesDir : ImportSourceKind.CursorRuleFile;
        }

        return type switch
        {
            TargetIds.AgentsMd => ImportSourceKind.AgentsMd,
            TargetIds.ClaudeMd => ImportSourceKind.ClaudeMd,
            TargetIds.Copilot => ImportSourceKind.Copilot,
            TargetIds.Gemini => ImportSourceKind.Gemini,
            _ => source.Kind,
        };
    }

    // --- skills root ----------------------------------------------------------

    private ImportReport ImportSkillsRoot(ImportSource source, AgentImportOptions options)
    {
        var items = new List<ImportItem>();
        foreach (var subdir in Directory.EnumerateDirectories(source.AbsolutePath).OrderBy(d => d, StringComparer.Ordinal))
        {
            var relative = _layout.Relative(subdir);
            ImportSource sub;
            try
            {
                sub = SourceDetector.Detect(_layout.RepoRoot, relative);
            }
            catch (RepoPathException ex)
            {
                items.Add(SkipItem(Path.GetFileName(subdir), relative, ex.Message));
                continue;
            }

            if (sub.Kind != ImportSourceKind.SkillFolder)
            {
                continue; // not a skill folder; ignore stray files
            }

            items.Add(BuildAndApplySkillFolder(sub, options));
        }

        return Aggregate(items, options.DryRun, source.RelativePath, "no skill folders found under");
    }

    private ImportItem BuildAndApplySkillFolder(ImportSource sub, AgentImportOptions options)
    {
        ParsedSkill parsed;
        try
        {
            parsed = SkillFolderReader.Read(sub);
        }
        catch (Exception ex) when (ex is ConfigParseException or FileNotFoundException)
        {
            return SkipItem(Path.GetFileName(sub.AbsolutePath), sub.RelativePath, ex.Message);
        }

        var draft = _skillImporter.BuildDraft(parsed, sub, new SkillImportOptions(Force: options.Force, DryRun: options.DryRun), out var idError);
        if (draft is null)
        {
            return SkipItem(Path.GetFileName(sub.AbsolutePath), sub.RelativePath, idError!);
        }

        return _skillImporter.ApplyDraft(draft, options.Force, options.DryRun);
    }

    // --- cursor ---------------------------------------------------------------

    private ImportReport ImportCursorDir(ImportSource source, AgentImportOptions options)
    {
        var files = Directory.EnumerateFiles(source.AbsolutePath)
            .Where(f => string.Equals(Path.GetExtension(f), ".mdc", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
        return ImportCursorFiles(files, options);
    }

    private ImportReport ImportCursorFiles(IReadOnlyList<string> files, AgentImportOptions options)
    {
        var items = new List<ImportItem>();
        foreach (var file in files)
        {
            var relative = _layout.Relative(file);
            var content = File.ReadAllText(file);
            var split = MarkdownFrontmatter.Split(content);

            SkillManifest fm;
            try
            {
                fm = split.HasFrontmatter ? SkillManifest.Parse(split.Frontmatter!) : new SkillManifest();
            }
            catch (ConfigParseException ex)
            {
                items.Add(SkipItem(Path.GetFileNameWithoutExtension(file), relative, ex.Message));
                continue;
            }

            var (heading, body) = SplitLeadingHeading(split.Body);
            var name = heading ?? Path.GetFileNameWithoutExtension(file);
            var id = (files.Count == 1 ? options.Id : null) ?? IdInference.Slugify(name) ?? IdInference.Slugify(Path.GetFileNameWithoutExtension(file));
            if (id is null)
            {
                items.Add(SkipItem(Path.GetFileNameWithoutExtension(file), relative, "Could not infer a valid id; pass --id."));
                continue;
            }

            var description = string.IsNullOrWhiteSpace(fm.Description) ? string.Empty : fm.Description!.Trim();
            var note = "Cursor globs/alwaysApply are not part of the canonical schema and were dropped.";
            var draft = new SkillDraft(id, name, description, "0.1.0",
                SkillContent.StripRedundantHeading(body, name), new[] { TargetIds.Cursor }, relative);
            items.Add(_skillImporter.ApplyDraft(draft, options.Force, options.DryRun) with { Note = note });
        }

        return Aggregate(items, options.DryRun, "the Cursor rules", "no .mdc files found in");
    }

    // --- shared markdown ------------------------------------------------------

    private ImportReport ImportSharedMarkdown(ImportSource source, ImportSourceKind kind, AgentImportOptions options)
    {
        var content = File.ReadAllText(source.AbsolutePath);
        var hand = options.IncludeGenerated ? content : StripGenerated(content);
        var target = TargetForKind(kind);

        var items = new List<ImportItem>();

        if (options.Split == "sections")
        {
            foreach (var section in HeadingSplitter.Split(hand))
            {
                if (section.Title is null)
                {
                    continue; // preamble before the first heading is skipped in section mode
                }

                var id = IdInference.Slugify(section.Title);
                if (id is null)
                {
                    items.Add(SkipItem(section.Title, source.RelativePath, "Heading does not yield a valid id; pass --split file or rename."));
                    continue;
                }

                var description = FirstParagraph(section.Body);
                var draft = new SkillDraft(id, section.Title, description, "0.1.0",
                    SkillContent.StripRedundantHeading(section.Body, section.Title), new[] { target }, source.RelativePath);
                items.Add(_skillImporter.ApplyDraft(draft, options.Force, options.DryRun));
            }

            return Aggregate(items, options.DryRun, source.RelativePath, "no headings found in");
        }

        // --split file (default): one skill for the whole hand-authored body.
        var (heading, _) = SplitLeadingHeading(hand);
        var fileId = options.Id ?? IdInference.Slugify(Path.GetFileNameWithoutExtension(source.AbsolutePath));
        if (fileId is null)
        {
            return ImportReport.Failure(ImportStatus.InvalidSource,
                $"Could not infer an id from '{source.RelativePath}'. Pass --id.", options.DryRun);
        }

        var fileName = heading ?? TitleCase(fileId);
        var fileDescription = FirstParagraph(hand);
        var fileDraft = new SkillDraft(fileId, fileName, fileDescription, "0.1.0",
            SkillContent.StripRedundantHeading(hand, fileName), new[] { target }, source.RelativePath);
        items.Add(_skillImporter.ApplyDraft(fileDraft, options.Force, options.DryRun));

        return Aggregate(items, options.DryRun, source.RelativePath, "nothing to import in");
    }

    // --- helpers --------------------------------------------------------------

    private static string TargetForKind(ImportSourceKind kind) => kind switch
    {
        ImportSourceKind.AgentsMd => TargetIds.AgentsMd,
        ImportSourceKind.ClaudeMd => TargetIds.ClaudeMd,
        ImportSourceKind.Copilot => TargetIds.Copilot,
        ImportSourceKind.Gemini => TargetIds.Gemini,
        _ => TargetIds.AgentsMd,
    };

    private ImportItem SkipItem(string id, string sourceRelative, string note)
        => new(id, id, string.Empty, string.Empty, string.Empty, ImportAction.Skip, Array.Empty<ValidationMessage>(), sourceRelative, note);

    private ImportReport Aggregate(IReadOnlyList<ImportItem> items, bool dryRun, string sourceDisplay, string emptyVerb)
    {
        if (items.Count == 0)
        {
            return ImportReport.Failure(ImportStatus.InvalidSource, $"{char.ToUpperInvariant(emptyVerb[0])}{emptyVerb[1..]} '{sourceDisplay}'.", dryRun);
        }

        var problem = items.Any(i => i.Action == ImportAction.Skip || i.HasValidationErrors);
        return new ImportReport(problem ? ImportStatus.Problem : ImportStatus.Ok, items, dryRun);
    }

    /// <summary>Removes <c>agent-sync</c> generated sections, leaving only hand-authored text.</summary>
    internal static string StripGenerated(string content)
    {
        var sb = new System.Text.StringBuilder();
        var pos = 0;
        while (pos < content.Length)
        {
            var start = Markers.StartMarker().Match(content, pos);
            if (!start.Success)
            {
                sb.Append(content[pos..]);
                break;
            }

            var end = Markers.EndMarker().Match(content, start.Index + start.Length);
            if (!end.Success)
            {
                sb.Append(content[pos..]);
                break;
            }

            sb.Append(content[pos..start.Index]);
            pos = end.Index + end.Length;
        }

        return sb.ToString();
    }

    private static (string? Heading, string Body) SplitLeadingHeading(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n").TrimStart('\n');
        var newline = normalized.IndexOf('\n');
        var first = newline < 0 ? normalized : normalized[..newline];
        if (first.StartsWith("# ", StringComparison.Ordinal))
        {
            var rest = newline < 0 ? string.Empty : normalized[(newline + 1)..];
            return (first[2..].Trim(), rest.TrimStart('\n'));
        }

        return (null, markdown);
    }

    private static string FirstParagraph(string body)
    {
        foreach (var raw in body.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            return line;
        }

        return string.Empty;
    }

    private static string TitleCase(string id)
        => string.Join(' ', id.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
}
