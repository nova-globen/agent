namespace AgentSync.Core.Import;

/// <summary>
/// Classifies an import source path into an <see cref="ImportSource"/>. Every path is
/// resolved through <see cref="RepoPath"/> so absolute, Windows drive/UNC, and
/// <c>..</c>-escaping paths are rejected (throws <see cref="RepoPathException"/>).
/// </summary>
public static class SourceDetector
{
    private const string SkillFileName = "SKILL.md";

    /// <summary>
    /// Detects the shape of <paramref name="rawPath"/> relative to
    /// <paramref name="repoRoot"/>. Throws <see cref="RepoPathException"/> when the path
    /// is unsafe (escapes the repository root).
    /// </summary>
    public static ImportSource Detect(string repoRoot, string rawPath)
    {
        var relative = Normalize(repoRoot, rawPath);
        var absolute = RepoPath.Resolve(repoRoot, relative);
        var displayRelative = new RepoLayout(repoRoot).Relative(absolute);

        if (Directory.Exists(absolute))
        {
            return DetectDirectory(absolute, displayRelative);
        }

        if (File.Exists(absolute))
        {
            return DetectFile(absolute, displayRelative);
        }

        return new ImportSource(ImportSourceKind.Missing, absolute, displayRelative, $"path '{displayRelative}' does not exist.");
    }

    private static ImportSource DetectDirectory(string absolute, string relative)
    {
        if (ContainsSkillFile(absolute))
        {
            return new ImportSource(ImportSourceKind.SkillFolder, absolute, relative);
        }

        var name = Path.GetFileName(absolute.TrimEnd(Path.DirectorySeparatorChar, '/'));

        if (string.Equals(name, "skills", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportSource(ImportSourceKind.SkillsRoot, absolute, relative);
        }

        if (IsCursorRulesDir(relative) || HasMdcFiles(absolute))
        {
            return new ImportSource(ImportSourceKind.CursorRulesDir, absolute, relative);
        }

        return new ImportSource(
            ImportSourceKind.Unsupported,
            absolute,
            relative,
            $"directory '{relative}' has no SKILL.md and is not a recognized skills or Cursor rules folder.");
    }

    private static ImportSource DetectFile(string absolute, string relative)
    {
        var name = Path.GetFileName(absolute);

        if (string.Equals(name, SkillFileName, StringComparison.OrdinalIgnoreCase))
        {
            return new ImportSource(ImportSourceKind.SkillFile, absolute, relative);
        }

        if (string.Equals(Path.GetExtension(name), ".mdc", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportSource(ImportSourceKind.CursorRuleFile, absolute, relative);
        }

        if (string.Equals(name, "AGENTS.md", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportSource(ImportSourceKind.AgentsMd, absolute, relative);
        }

        if (string.Equals(name, "CLAUDE.md", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportSource(ImportSourceKind.ClaudeMd, absolute, relative);
        }

        if (string.Equals(name, "copilot-instructions.md", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportSource(ImportSourceKind.Copilot, absolute, relative);
        }

        if (string.Equals(name, "GEMINI.md", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportSource(ImportSourceKind.Gemini, absolute, relative);
        }

        return new ImportSource(
            ImportSourceKind.Unsupported,
            absolute,
            relative,
            $"file '{relative}' is not a recognized skill or instruction file. Use --type to import a generic Markdown file as an instruction source.");
    }

    private static bool ContainsSkillFile(string dir)
        => Directory.EnumerateFiles(dir)
            .Any(f => string.Equals(Path.GetFileName(f), SkillFileName, StringComparison.OrdinalIgnoreCase));

    private static bool HasMdcFiles(string dir)
        => Directory.EnumerateFiles(dir).Any(f => string.Equals(Path.GetExtension(f), ".mdc", StringComparison.OrdinalIgnoreCase));

    private static bool IsCursorRulesDir(string relative)
    {
        var normalized = relative.Replace('\\', '/').TrimEnd('/');
        return normalized.EndsWith(".cursor/rules", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(".cursor/rules", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Turns a user-supplied path into a repository-relative path. An absolute path that
    /// lies under the repo root is rebased to relative; anything else is passed through
    /// to <see cref="RepoPath"/>, which rejects unsafe inputs.
    /// </summary>
    private static string Normalize(string repoRoot, string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return rawPath;
        }

        if (Path.IsPathRooted(rawPath))
        {
            var full = Path.GetFullPath(rawPath);
            var root = Path.GetFullPath(repoRoot);
            return Path.GetRelativePath(root, full).Replace('\\', '/');
        }

        return rawPath;
    }
}
