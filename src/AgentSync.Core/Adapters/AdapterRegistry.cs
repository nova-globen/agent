using AgentSync.Core.Configuration;

namespace AgentSync.Core.Adapters;

/// <summary>Maps target ids to their adapters.</summary>
public sealed class AdapterRegistry
{
    private readonly Dictionary<string, ISkillAdapter> _adapters;

    public AdapterRegistry(IEnumerable<ISkillAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(a => a.TargetId, StringComparer.Ordinal);
    }

    public static AdapterRegistry CreateDefault() => new(new ISkillAdapter[]
    {
        new SharedMarkdownAdapter(TargetIds.AgentsMd),
        new SharedMarkdownAdapter(TargetIds.ClaudeMd),
        new SharedMarkdownAdapter(TargetIds.Copilot),
        new SharedMarkdownAdapter(TargetIds.Gemini),
        new CursorAdapter(),
        new SkillFolderAdapter(TargetIds.OpenAiSkill),
        new SkillFolderAdapter(TargetIds.ClaudeSkill),
    });

    public ISkillAdapter? Get(string targetId)
        => _adapters.TryGetValue(targetId, out var adapter) ? adapter : null;

    public bool Has(string targetId) => _adapters.ContainsKey(targetId);
}
