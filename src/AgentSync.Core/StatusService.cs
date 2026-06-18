namespace AgentSync.Core;

public enum IssueSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record StatusIssue(string Code, IssueSeverity Severity, string Message);

public sealed record StatusReport(
    bool Initialized,
    int SkillCount,
    IReadOnlyList<StatusIssue> Issues)
{
    /// <summary>
    /// True when the repository is in an invalid or drifted state that CI should reject.
    /// In Milestone 1 this covers structural problems (missing config or lockfile);
    /// projection-level drift is added in later milestones.
    /// </summary>
    public bool HasProblems => Issues.Any(i => i.Severity == IssueSeverity.Error);
}

/// <summary>
/// Reports the current Agent Sync state for a repository. Milestone 1 reports
/// structural state (initialized? config present? lockfile present? skills found?).
/// </summary>
public sealed class StatusService
{
    private readonly RepoLayout _layout;

    public StatusService(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public StatusReport Run()
    {
        var issues = new List<StatusIssue>();
        var configExists = File.Exists(_layout.ConfigFile);
        var lockExists = File.Exists(_layout.LockFile);
        var initialized = Directory.Exists(_layout.AgentDir) && configExists;

        if (!initialized)
        {
            issues.Add(new StatusIssue(
                "not-initialized",
                IssueSeverity.Error,
                $"Agent Sync is not initialized. Run 'agent init' (expected {RepoLayout.AgentDirName}/{RepoLayout.ConfigFileName})."));

            return new StatusReport(false, 0, issues);
        }

        if (!lockExists)
        {
            issues.Add(new StatusIssue(
                "missing-lockfile",
                IssueSeverity.Error,
                $"Missing lockfile {_layout.Relative(_layout.LockFile)}. Run 'agent init'."));
        }

        var skillCount = CountSkills();
        if (skillCount == 0)
        {
            issues.Add(new StatusIssue(
                "no-skills",
                IssueSeverity.Warning,
                $"No canonical skills found under {RepoLayout.AgentDirName}/{RepoLayout.SkillsDirName}/."));
        }

        return new StatusReport(true, skillCount, issues);
    }

    private int CountSkills()
    {
        if (!Directory.Exists(_layout.SkillsDir))
        {
            return 0;
        }

        var count = 0;
        foreach (var dir in Directory.EnumerateDirectories(_layout.SkillsDir))
        {
            if (File.Exists(Path.Combine(dir, "skill.yaml")))
            {
                count++;
            }
        }

        return count;
    }
}
