namespace AgentSync.Core;

public sealed record DoctorCheck(string Name, bool Ok, string Detail);

public sealed record DoctorReport(IReadOnlyList<DoctorCheck> Checks)
{
    public bool AllOk => Checks.All(c => c.Ok);
}

/// <summary>
/// Environment facts gathered by the CLI and handed to <see cref="DoctorService"/>
/// so the diagnostic logic stays deterministic and testable.
/// </summary>
public sealed record DoctorInput(
    string? RepoRoot,
    string? HooksPath,
    bool AgentOnPath);

/// <summary>
/// Runs environment diagnostics: Git repo, PATH, hooks wiring, config, and lockfile.
/// </summary>
public sealed class DoctorService
{
    public DoctorReport Run(DoctorInput input)
    {
        var checks = new List<DoctorCheck>();

        var inRepo = input.RepoRoot is not null;
        checks.Add(new DoctorCheck(
            "Git repository",
            inRepo,
            inRepo ? $"Found repository root at {input.RepoRoot}" : "Not inside a Git repository."));

        checks.Add(new DoctorCheck(
            "agent on PATH",
            input.AgentOnPath,
            input.AgentOnPath
                ? "'agent' command is available on PATH."
                : "'agent' is not on PATH; Git hooks will fail until it is installed."));

        if (!inRepo)
        {
            return new DoctorReport(checks);
        }

        var layout = new RepoLayout(input.RepoRoot!);

        var configExists = File.Exists(layout.ConfigFile);
        checks.Add(new DoctorCheck(
            "Configuration",
            configExists,
            configExists
                ? $"Found {layout.Relative(layout.ConfigFile)}."
                : $"Missing {RepoLayout.AgentDirName}/{RepoLayout.ConfigFileName}. Run 'agent init'."));

        var lockExists = File.Exists(layout.LockFile);
        checks.Add(new DoctorCheck(
            "Lockfile",
            lockExists,
            lockExists
                ? $"Found {layout.Relative(layout.LockFile)}."
                : $"Missing {RepoLayout.AgentDirName}/{RepoLayout.LockFileName}. Run 'agent init'."));

        var hooksConfigured = string.Equals(
            NormalizeHooksPath(input.HooksPath),
            RepoLayout.DefaultHooksPath,
            StringComparison.Ordinal);
        var hooksPresent = File.Exists(layout.PreCommitHook) && File.Exists(layout.PrePushHook);
        var hooksOk = hooksConfigured && hooksPresent;
        checks.Add(new DoctorCheck(
            "Git hooks",
            hooksOk,
            hooksOk
                ? $"core.hooksPath is '{RepoLayout.DefaultHooksPath}' and hook scripts are present."
                : DescribeHookProblem(hooksConfigured, hooksPresent)));

        return new DoctorReport(checks);
    }

    private static string? NormalizeHooksPath(string? value)
        => value?.Trim().TrimEnd('/', '\\');

    private static string DescribeHookProblem(bool configured, bool present)
    {
        if (!configured && !present)
        {
            return "Hooks not installed. Run 'agent install-hooks'.";
        }

        if (!configured)
        {
            return $"core.hooksPath is not set to '{RepoLayout.DefaultHooksPath}'. Run 'agent install-hooks'.";
        }

        return "Hook scripts are missing. Run 'agent init'.";
    }
}
