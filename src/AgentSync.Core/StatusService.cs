using AgentSync.Core.Configuration;
using AgentSync.Core.Drift;

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
    /// </summary>
    public bool HasProblems => Issues.Any(i => i.Severity == IssueSeverity.Error);
}

/// <summary>
/// Reports the current Agent Sync state for a repository: structural checks plus full
/// drift detection (missing/outdated/manually edited projections, invalid config,
/// missing or orphaned lockfile entries).
/// </summary>
public sealed class StatusService
{
    private readonly RepoLayout _layout;

    public StatusService(string repoRoot) => _layout = new RepoLayout(repoRoot);

    public StatusReport Run()
    {
        var issues = new List<StatusIssue>();
        var lockExists = File.Exists(_layout.LockFile);
        var configExists = File.Exists(_layout.ConfigFile);
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

        AddDriftIssues(issues);

        return new StatusReport(true, skillCount, issues);
    }

    private void AddDriftIssues(List<StatusIssue> issues)
    {
        var report = new DriftDetector(_layout.RepoRoot).Detect();

        if (!report.ConfigValid)
        {
            foreach (var m in report.Validation.Messages.Where(m => m.Severity == ValidationSeverity.Error))
            {
                issues.Add(new StatusIssue("config-error", IssueSeverity.Error,
                    string.IsNullOrEmpty(m.Source) ? m.Message : $"{m.Source}: {m.Message}"));
            }
            return;
        }

        foreach (var item in report.Drifted)
        {
            var (code, message) = item.Kind switch
            {
                DriftKind.Missing => ("drift-missing", $"Missing projection {item.RelativePath} ({item.TargetId})."),
                DriftKind.Outdated => ("drift-outdated", $"Outdated projection {item.RelativePath} ({item.TargetId}). Run 'agent sync'."),
                DriftKind.ManualEdit => ("drift-manual-edit", $"Manually edited projection {item.RelativePath} ({item.TargetId}). Run 'agent sync --force' to regenerate."),
                _ => ("drift-lock", $"Lockfile entry mismatch for {item.RelativePath} ({item.TargetId}). Run 'agent sync'."),
            };
            issues.Add(new StatusIssue(code, IssueSeverity.Error, message));
        }

        foreach (var orphan in report.Orphans)
        {
            issues.Add(new StatusIssue("drift-orphan", IssueSeverity.Error,
                $"Lockfile references a projection no longer planned: {orphan.Path} ({orphan.Target})."));
        }
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
