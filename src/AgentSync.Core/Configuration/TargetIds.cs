namespace AgentSync.Core.Configuration;

/// <summary>
/// The known projection target identifiers used as keys in <c>agent.yaml</c> and
/// <c>skill.yaml</c>.
/// </summary>
public static class TargetIds
{
    public const string AgentsMd = "agents_md";
    public const string ClaudeMd = "claude_md";
    public const string Cursor = "cursor";
    public const string Copilot = "copilot";
    public const string Gemini = "gemini";
    public const string OpenAiSkill = "openai_skill";
    public const string ClaudeSkill = "claude_skill";

    /// <summary>
    /// Configurable sub-agent projection target that emits TOML; opt-in via <c>agent.yaml</c>
    /// <c>targets:</c> alongside skill targets. Not processed by <see cref="ProjectionPlanner"/>
    /// (which only handles skill targets); <c>SubagentProjector</c> reads it instead.
    /// </summary>
    public const string TomlAgent = "toml_agent";

    /// <summary>Skill targets in canonical order (used by ProjectionPlanner).</summary>
    public static readonly IReadOnlyList<string> Ordered = new[]
    {
        AgentsMd,
        ClaudeMd,
        Cursor,
        Copilot,
        Gemini,
        OpenAiSkill,
        ClaudeSkill,
    };

    /// <summary>All known configurable targets (skill targets + agent targets).</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        Ordered.Concat(new[] { TomlAgent }),
        StringComparer.Ordinal);

    public static bool IsKnown(string id) => All.Contains(id);
}
