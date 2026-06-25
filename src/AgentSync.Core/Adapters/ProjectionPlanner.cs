using AgentSync.Core.Configuration;

namespace AgentSync.Core.Adapters;

/// <summary>
/// Enumerates the projections to produce for a workspace: one per (skill, target)
/// where the target is enabled in both the skill manifest and the repo config.
/// Output ordering is deterministic (skills by id, targets by canonical order).
/// </summary>
public sealed class ProjectionPlanner
{
    private readonly AdapterRegistry _registry;

    public ProjectionPlanner(AdapterRegistry? registry = null)
        => _registry = registry ?? AdapterRegistry.CreateDefault();

    public IReadOnlyList<Projection> Plan(Workspace workspace)
    {
        var config = workspace.Config;
        if (config is null)
        {
            return Array.Empty<Projection>();
        }

        var projections = new List<Projection>();

        foreach (var skill in workspace.Skills.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            foreach (var targetId in TargetIds.Ordered)
            {
                if (!SkillEnables(skill, targetId))
                {
                    continue;
                }

                if (!config.Targets.TryGetValue(targetId, out var targetSetting)
                    || !targetSetting.Enabled
                    || string.IsNullOrWhiteSpace(targetSetting.Path))
                {
                    continue;
                }

                var adapter = _registry.Get(targetId);
                if (adapter is null)
                {
                    continue;
                }

                var path = adapter.ResolvePath(targetSetting.Path!, skill);
                var body = adapter.Render(skill);
                var assetDir = adapter.Mode == ProjectionMode.WholeFile ? adapter.AssetSourceDir(skill) : null;
                projections.Add(new Projection(skill.Id, targetId, adapter.Mode, path, body, assetDir));
            }
        }

        return projections;
    }

    private static bool SkillEnables(Skill skill, string targetId)
        => skill.Manifest.Targets.TryGetValue(targetId, out var setting) && setting.Enabled;
}
