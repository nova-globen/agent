using AgentSync.Core.Configuration;

namespace AgentSync.Core.Import;

/// <summary>A skill source parsed into its manifest metadata, body, and an id candidate.</summary>
public sealed record ParsedSkill(
    SkillManifest Manifest,
    string Body,
    string? IdCandidate);

/// <summary>
/// Reads a skill folder or standalone <c>SKILL.md</c> into a <see cref="ParsedSkill"/>:
/// it splits any YAML frontmatter (parsed with the same deserializer as
/// <see cref="SkillManifest"/>) from the Markdown body. Throws
/// <see cref="ConfigParseException"/> on malformed frontmatter.
/// </summary>
public static class SkillFolderReader
{
    private const string SkillFileName = "SKILL.md";

    /// <summary>Reads from a detected skill source (folder or bare file).</summary>
    public static ParsedSkill Read(ImportSource source)
    {
        return source.Kind switch
        {
            ImportSourceKind.SkillFolder => ReadFolder(source.AbsolutePath),
            ImportSourceKind.SkillFile => ReadFile(source.AbsolutePath, FolderNameOf(Path.GetDirectoryName(source.AbsolutePath))),
            _ => throw new InvalidOperationException($"SkillFolderReader cannot read a source of kind '{source.Kind}'."),
        };
    }

    /// <summary>Reads a skill folder, locating its <c>SKILL.md</c> (case-insensitive).</summary>
    public static ParsedSkill ReadFolder(string folderAbsolutePath)
    {
        var skillFile = Directory.EnumerateFiles(folderAbsolutePath)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), SkillFileName, StringComparison.OrdinalIgnoreCase));

        if (skillFile is null)
        {
            throw new FileNotFoundException($"No {SkillFileName} found in '{folderAbsolutePath}'.");
        }

        return ReadFile(skillFile, FolderNameOf(folderAbsolutePath));
    }

    /// <summary>Reads a <c>SKILL.md</c> file; <paramref name="folderName"/> is the default id candidate.</summary>
    public static ParsedSkill ReadFile(string skillMdAbsolutePath, string? folderName)
    {
        var content = File.ReadAllText(skillMdAbsolutePath);
        var split = MarkdownFrontmatter.Split(content);

        var manifest = split.HasFrontmatter
            ? SkillManifest.Parse(split.Frontmatter!)
            : new SkillManifest();

        var idCandidate =
            (IdInference.IsValid(manifest.Id) ? manifest.Id : null)
            ?? IdInference.Slugify(folderName)
            ?? IdInference.Slugify(manifest.Name);

        return new ParsedSkill(manifest, split.Body, idCandidate);
    }

    private static string? FolderNameOf(string? path)
        => string.IsNullOrEmpty(path) ? null : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, '/'));
}
