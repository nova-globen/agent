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

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        AgentsMd,
        ClaudeMd,
        Cursor,
        Copilot,
        Gemini,
        OpenAiSkill,
        ClaudeSkill,
    };

    public static bool IsKnown(string id) => All.Contains(id);
}
