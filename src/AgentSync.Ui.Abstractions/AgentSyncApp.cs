using AgentSync.Core;
using AgentSync.Core.Authoring;
using AgentSync.Core.Configuration;
using AgentSync.Core.Drift;
using AgentSync.Core.Import;

namespace AgentSync.Ui.Abstractions;

/// <summary>A snapshot of a repository's Agent Sync state, for a UI dashboard.</summary>
public sealed record RepositoryState(
    string RepoPath,
    bool IsGitRepository,
    bool Initialized,
    int SkillCount,
    bool HasProblems);

/// <summary>
/// UI-independent application service over <see cref="AgentSync.Core"/>. The desktop GUI
/// (and any other shell) drives Agent Sync through this facade so that no repository
/// logic lives in Razor components and no business logic is duplicated from the CLI.
/// </summary>
/// <remarks>
/// This type has no MAUI/OpenMaui dependency and is fully unit-testable without a
/// renderer. Mutating methods (Sync with force, AddSkill, DeleteSkill, target edits)
/// must only be called after the UI obtains explicit user confirmation.
/// </remarks>
public sealed class AgentSyncApp
{
    public AgentSyncApp(string startDirectory)
    {
        RepoRoot = GitRepository.Discover(startDirectory) ?? startDirectory;
        IsGitRepository = GitRepository.Discover(startDirectory) is not null;
    }

    /// <summary>The resolved repository root the app operates on.</summary>
    public string RepoRoot { get; }

    /// <summary>True if <see cref="RepoRoot"/> is inside a Git repository.</summary>
    public bool IsGitRepository { get; }

    // --- read-only views ------------------------------------------------------

    public RepositoryState GetState()
    {
        var report = new StatusService(RepoRoot).Run();
        return new RepositoryState(RepoRoot, IsGitRepository, report.Initialized, report.SkillCount, report.HasProblems);
    }

    public IReadOnlyList<Skill> ListSkills()
        => WorkspaceLoader.Load(RepoRoot).Skills.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();

    public Skill? GetSkill(string id)
        => WorkspaceLoader.Load(RepoRoot).Skills.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    public AgentConfig? GetConfig() => WorkspaceLoader.Load(RepoRoot).Config;

    public StatusReport Status() => new StatusService(RepoRoot).Run();

    public DriftReport Drift() => new DriftDetector(RepoRoot).Detect();

    public DiffReport Diff() => new DiffService(RepoRoot).Run();

    public ValidationResult Validate() => WorkspaceLoader.Load(RepoRoot).Validation;

    // --- mutations (UI must confirm before calling) ---------------------------

    public SyncReport Sync(bool force = false, bool dryRun = false)
        => new SyncService(RepoRoot).Run(force, dryRun);

    public ImportReport ImportSkill(string path, SkillImportOptions options)
        => new SkillImporter(RepoRoot).Import(path, options);

    public ImportReport ImportAgent(string path, AgentImportOptions options)
        => new AgentImporter(RepoRoot).Import(path, options);

    public AuthoringResult AddSkill(string id, string? name, string? description, string? version, IReadOnlyList<string>? targets)
        => new SkillWriter(RepoRoot).Add(id, name, description, version, targets);

    public AuthoringResult EditSkill(string id, string? name, string? description, string? version, string? bodyFile, IReadOnlyList<string>? enable, IReadOnlyList<string>? disable)
        => new SkillWriter(RepoRoot).Edit(id, name, description, version, bodyFile, enable, disable);

    public AuthoringResult DeleteSkill(string id, bool force, bool dryRun)
        => new SkillWriter(RepoRoot).Delete(id, force, dryRun);

    public AuthoringResult AddTarget(string id, string? path, bool enabled)
        => new TargetWriter(RepoRoot).Add(id, path, enabled);

    public AuthoringResult EditTarget(string id, string? path, bool? enabled)
        => new TargetWriter(RepoRoot).Edit(id, path, enabled);

    public AuthoringResult DeleteTarget(string id, bool force, bool dryRun)
        => new TargetWriter(RepoRoot).Delete(id, force, dryRun);

    /// <summary>The CI command users can copy from the GUI.</summary>
    public static string CiCommand => "agent status --fail-on-drift --ci";
}
