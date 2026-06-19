namespace AgentSync.Core.Import;

/// <summary>The recognized shape of an import source path.</summary>
public enum ImportSourceKind
{
    /// <summary>A directory containing a <c>SKILL.md</c> (a skill folder).</summary>
    SkillFolder,

    /// <summary>A standalone <c>SKILL.md</c> file.</summary>
    SkillFile,

    /// <summary>A skills root such as <c>.chatgpt/skills</c> or <c>.claude/skills</c> (many skills).</summary>
    SkillsRoot,

    /// <summary>An <c>AGENTS.md</c> shared instruction file.</summary>
    AgentsMd,

    /// <summary>A <c>CLAUDE.md</c> shared instruction file.</summary>
    ClaudeMd,

    /// <summary>A GitHub Copilot instructions file.</summary>
    Copilot,

    /// <summary>A Gemini instructions file.</summary>
    Gemini,

    /// <summary>A single Cursor rule file (<c>.mdc</c>).</summary>
    CursorRuleFile,

    /// <summary>A Cursor rules directory (<c>.cursor/rules</c>) containing <c>.mdc</c> files.</summary>
    CursorRulesDir,

    /// <summary>The path does not exist.</summary>
    Missing,

    /// <summary>The path exists but its shape is not recognized.</summary>
    Unsupported,
}

/// <summary>
/// A detected import source: its recognized <see cref="Kind"/>, the resolved absolute
/// path, and the repository-relative path for display. <see cref="Reason"/> explains a
/// <see cref="ImportSourceKind.Missing"/> or <see cref="ImportSourceKind.Unsupported"/>
/// result.
/// </summary>
public sealed record ImportSource(
    ImportSourceKind Kind,
    string AbsolutePath,
    string RelativePath,
    string? Reason = null)
{
    /// <summary>True if this source is skill-shaped (folder or bare <c>SKILL.md</c>).</summary>
    public bool IsSkill => Kind is ImportSourceKind.SkillFolder or ImportSourceKind.SkillFile;

    /// <summary>True if this source is a shared Markdown instruction file.</summary>
    public bool IsSharedMarkdown =>
        Kind is ImportSourceKind.AgentsMd or ImportSourceKind.ClaudeMd
            or ImportSourceKind.Copilot or ImportSourceKind.Gemini;
}
