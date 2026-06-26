namespace AgentSync.Core.Autopilot;

/// <summary>Issue severity emitted by the autopilot pre-flight check.</summary>
public enum PreflightIssueSeverity
{
    /// <summary>Autopilot may have limited functionality but can continue.</summary>
    Warning,
    /// <summary>Autopilot cannot function correctly without remediation.</summary>
    Blocker,
}

/// <summary>A single finding from the autopilot pre-flight check.</summary>
public sealed record PreflightIssue(
    string Code,
    PreflightIssueSeverity Severity,
    string Message,
    string? FixCommand = null);

/// <summary>Aggregate result of the autopilot pre-flight check.</summary>
public sealed record AutopilotPreflightResult(
    IReadOnlyList<PreflightIssue> Issues)
{
    public bool HasBlockers => Issues.Any(i => i.Severity == PreflightIssueSeverity.Blocker);
    public bool HasIssues   => Issues.Count > 0;
}

/// <summary>
/// Verifies that the current repository has the agent-platform infrastructure required
/// by <c>agent autopilot</c> before the loop starts.
/// <para>
/// Checks performed (in order):
/// <list type="bullet">
///   <item>AgentSync initialized (<c>.agent/agent.yaml</c> present) — Blocker if missing.</item>
///   <item>Autopilot skill registered (canonical <c>.agent/skills/autopilot/</c> or projected
///   <c>.claude/skills/autopilot/SKILL.md</c>) — Warning if absent.</item>
///   <item>Prompts directory exists (<c>.agent/prompts/autopilot/</c>) — Warning if absent.</item>
///   <item>At least one handoff prompt (<c>prompt-*.txt</c>) present — Warning if none found.</item>
///   <item>AgentSync projections are current (no drift) — Warning if drift detected.</item>
/// </list>
/// </para>
/// </summary>
public sealed class AutopilotPreflightChecker
{
    private readonly string _root;

    public AutopilotPreflightChecker(string root) => _root = root;

    public AutopilotPreflightResult Check()
    {
        var issues = new List<PreflightIssue>();

        // ── 1. AgentSync initialized? ─────────────────────────────────────────
        var agentYaml = Path.Combine(_root, ".agent", "agent.yaml");
        if (!File.Exists(agentYaml))
        {
            issues.Add(new PreflightIssue(
                "preflight.no-agent-yaml",
                PreflightIssueSeverity.Blocker,
                ".agent/agent.yaml not found — this directory is not an Agent Sync repository.",
                "agent init --with-samples"));
            // Cannot check anything else without the config.
            return new AutopilotPreflightResult(issues);
        }

        // ── 2. Autopilot skill present? ───────────────────────────────────────
        var canonicalSkill  = Path.Combine(_root, ".agent",  "skills", "autopilot");
        var projectedSkill  = Path.Combine(_root, ".claude", "skills", "autopilot", "SKILL.md");
        if (!Directory.Exists(canonicalSkill) && !File.Exists(projectedSkill))
        {
            issues.Add(new PreflightIssue(
                "preflight.no-autopilot-skill",
                PreflightIssueSeverity.Warning,
                "Autopilot skill not found in .agent/skills/ or .claude/skills/.",
                "agent init --with-samples"));
        }

        // ── 3. Prompts directory present? ─────────────────────────────────────
        var promptsDir = Path.Combine(_root, ".agent", "prompts", "autopilot");
        if (!Directory.Exists(promptsDir))
        {
            issues.Add(new PreflightIssue(
                "preflight.no-prompts-dir",
                PreflightIssueSeverity.Warning,
                ".agent/prompts/autopilot/ not found. Autopilot will start fresh (via next-step skill)."));
        }
        else if (!Directory.EnumerateFiles(promptsDir, "prompt-*.txt").Any())
        {
            issues.Add(new PreflightIssue(
                "preflight.no-prompts",
                PreflightIssueSeverity.Warning,
                "No handoff prompt (prompt-*.txt) found in .agent/prompts/autopilot/. Autopilot will start fresh."));
        }

        // ── 4. Drift? ─────────────────────────────────────────────────────────
        try
        {
            var report = new StatusService(_root).Run();
            if (report.HasProblems)
            {
                issues.Add(new PreflightIssue(
                    "preflight.drift",
                    PreflightIssueSeverity.Warning,
                    "AgentSync projections have drifted from canonical sources.",
                    "agent sync"));
            }
        }
        catch
        {
            // Non-fatal: drift detection failure does not block autopilot.
        }

        return new AutopilotPreflightResult(issues);
    }
}
