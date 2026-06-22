using AgentSync.Core.Configuration;
using AgentSync.Core.Subagents;

namespace AgentSync.Core.Import;

/// <summary>Options controlling a sub-agent import.</summary>
public sealed record SubagentImportOptions(string? Id, bool Force, bool DryRun);

/// <summary>
/// Imports existing agent-tool sub-agent definitions (Claude Code <c>.claude/agents/*.md</c>
/// files with YAML frontmatter) into canonical sub-agents under <c>.agent/agents/</c>. A
/// single file imports one sub-agent; a directory imports every <c>*.md</c> inside it.
/// </summary>
public sealed class SubagentImporter
{
    private readonly RepoLayout _layout;

    public SubagentImporter(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public ImportReport Import(string path, SubagentImportOptions options)
    {
        string absolute;
        try
        {
            absolute = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return ImportReport.Failure(ImportStatus.InvalidSource, ex.Message, options.DryRun);
        }

        List<string> files;
        if (File.Exists(absolute))
        {
            files = new List<string> { absolute };
        }
        else if (Directory.Exists(absolute))
        {
            files = Directory.EnumerateFiles(absolute, "*.md", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            if (files.Count == 0)
            {
                return ImportReport.Failure(ImportStatus.SourceNotFound, $"No .md sub-agent files found under '{path}'.", options.DryRun);
            }
        }
        else
        {
            return ImportReport.Failure(ImportStatus.SourceNotFound, $"Source '{path}' does not exist.", options.DryRun);
        }

        var items = new List<ImportItem>();
        var status = ImportStatus.Ok;

        foreach (var file in files)
        {
            var item = ImportOne(file, files.Count == 1 ? options.Id : null, options.Force, options.DryRun, out var itemStatus);
            items.Add(item);
            if (itemStatus != ImportStatus.Ok && status == ImportStatus.Ok)
            {
                status = itemStatus;
            }
        }

        return new ImportReport(status, items, options.DryRun);
    }

    private ImportItem ImportOne(string file, string? idOverride, bool force, bool dryRun, out ImportStatus status)
    {
        status = ImportStatus.Ok;
        var sourceRel = Path.GetRelativePath(_layout.RepoRoot, file).Replace('\\', '/');
        var content = File.ReadAllText(file);
        var split = MarkdownFrontmatter.Split(content);
        var fields = ParseFrontmatter(split.Frontmatter);

        var nameFromFile = Path.GetFileNameWithoutExtension(file);
        var id = idOverride
            ?? IdInference.Slugify(fields.GetValueOrDefault("name"))
            ?? IdInference.Slugify(nameFromFile);

        if (id is null || !IdInference.IsValid(id))
        {
            status = ImportStatus.Problem;
            return Skipped(sourceRel, nameFromFile, "Could not derive a valid kebab-case id; pass --id.");
        }

        var description = fields.GetValueOrDefault("description") ?? string.Empty;
        var model = fields.GetValueOrDefault("model");
        var tools = ParseTools(fields.GetValueOrDefault("tools"), split.Frontmatter);
        var name = fields.GetValueOrDefault("name") ?? id;

        var dir = SubagentFiles.AgentDir(_layout, id);
        var exists = Directory.Exists(dir);
        if (exists && !force)
        {
            status = ImportStatus.Problem;
            return new ImportItem(id, name, description,
                _layout.Relative(Path.Combine(dir, "agent.yaml")),
                _layout.Relative(Path.Combine(dir, "AGENT.md")),
                ImportAction.Skip, Array.Empty<ValidationMessage>(), sourceRel,
                "Already exists. Re-run with --force to overwrite.");
        }

        var validation = new List<ValidationMessage>();
        if (string.IsNullOrWhiteSpace(description))
        {
            validation.Add(new ValidationMessage("subagent.description-missing", ValidationSeverity.Warning,
                "Imported sub-agent has no description.", $"{id}/agent.yaml"));
        }

        if (!dryRun)
        {
            var yaml = SubagentFiles.RenderManifestYaml(id, name, description, model, tools);
            SubagentFiles.Write(_layout, id, yaml, split.Body);
        }

        return new ImportItem(id, name, description,
            _layout.Relative(Path.Combine(dir, "agent.yaml")),
            _layout.Relative(Path.Combine(dir, "AGENT.md")),
            exists ? ImportAction.Overwrite : ImportAction.Create, validation, sourceRel);
    }

    private ImportItem Skipped(string sourceRel, string name, string note)
        => new(name, name, string.Empty, string.Empty, string.Empty, ImportAction.Skip,
            new[] { new ValidationMessage("subagent.import-skip", ValidationSeverity.Error, note, sourceRel) },
            sourceRel, note);

    /// <summary>Light parser for the simple <c>key: value</c> lines of a sub-agent frontmatter.</summary>
    internal static Dictionary<string, string> ParseFrontmatter(string? frontmatter)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(frontmatter))
        {
            return fields;
        }

        foreach (var raw in frontmatter.Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0 || line[0] == '-' || line.StartsWith(' '))
            {
                continue; // list item or nested; handled separately for tools.
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = Unquote(line[(colon + 1)..].Trim());
            fields[key] = value;
        }

        return fields;
    }

    /// <summary>Parses a tools value that may be a comma-separated string or a YAML block list.</summary>
    internal static IReadOnlyList<string> ParseTools(string? inlineValue, string? frontmatter)
    {
        if (!string.IsNullOrWhiteSpace(inlineValue))
        {
            return inlineValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Unquote).ToList();
        }

        // Block list: lines after a "tools:" line that start with "- ".
        var tools = new List<string>();
        if (string.IsNullOrEmpty(frontmatter))
        {
            return tools;
        }

        var lines = frontmatter.Split('\n');
        var inTools = false;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("tools:", StringComparison.OrdinalIgnoreCase) && line.TrimEnd().EndsWith(':'))
            {
                inTools = true;
                continue;
            }

            if (inTools)
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    tools.Add(Unquote(trimmed[2..].Trim()));
                }
                else if (line.Length > 0 && !line.StartsWith(' '))
                {
                    break; // next top-level key
                }
            }
        }

        return tools;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}
