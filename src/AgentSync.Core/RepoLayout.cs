namespace AgentSync.Core;

/// <summary>
/// Canonical relative paths and absolute-path helpers for the Agent Sync layout.
/// </summary>
public sealed class RepoLayout
{
    public RepoLayout(string repoRoot)
    {
        RepoRoot = Path.GetFullPath(repoRoot);
    }

    public string RepoRoot { get; }

    public const string AgentDirName = ".agent";
    public const string SkillsDirName = "skills";
    public const string AgentsDirName = "agents";
    public const string ConfigFileName = "agent.yaml";
    public const string LockFileName = "lock.json";
    public const string AgentsLockFileName = "agents.lock.json";
    public const string HooksDirName = ".githooks";
    public const string DefaultHooksPath = ".githooks";

    /// <summary>Where projected Claude Code sub-agents are written.</summary>
    public const string ClaudeAgentsDir = ".claude/agents";

    public string AgentDir => Path.Combine(RepoRoot, AgentDirName);
    public string SkillsDir => Path.Combine(AgentDir, SkillsDirName);
    public string AgentsDir => Path.Combine(AgentDir, AgentsDirName);
    public string ConfigFile => Path.Combine(AgentDir, ConfigFileName);
    public string LockFile => Path.Combine(AgentDir, LockFileName);
    public string AgentsLockFile => Path.Combine(AgentDir, AgentsLockFileName);
    public string HooksDir => Path.Combine(RepoRoot, HooksDirName);
    public string PreCommitHook => Path.Combine(HooksDir, "pre-commit");
    public string PrePushHook => Path.Combine(HooksDir, "pre-push");

    /// <summary>Returns a path relative to the repository root for display.</summary>
    public string Relative(string absolutePath)
        => Path.GetRelativePath(RepoRoot, absolutePath).Replace('\\', '/');
}
