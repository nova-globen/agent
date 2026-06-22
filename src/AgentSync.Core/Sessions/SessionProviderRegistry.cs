using AgentSync.Core.Sessions.Providers;

namespace AgentSync.Core.Sessions;

/// <summary>
/// The set of supported session providers, resolvable by id or alias. Order is stable so the
/// CLI lists them deterministically.
/// </summary>
public sealed class SessionProviderRegistry
{
    private readonly IReadOnlyList<ISessionProvider> _providers;
    private readonly Dictionary<string, ISessionProvider> _byName;

    public SessionProviderRegistry(IReadOnlyList<ISessionProvider> providers)
    {
        _providers = providers;
        _byName = new Dictionary<string, ISessionProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in providers)
        {
            _byName[p.Id] = p;
            foreach (var alias in p.Aliases)
            {
                _byName[alias] = p;
            }
        }
    }

    /// <summary>The default registry with every built-in provider.</summary>
    public static SessionProviderRegistry Default { get; } = new(new ISessionProvider[]
    {
        new ClaudeSessionProvider(),
        new CodexSessionProvider(),
        new CopilotSessionProvider(),
        new GeminiSessionProvider(),
        new CursorSessionProvider(),
    });

    public IReadOnlyList<ISessionProvider> Providers => _providers;

    /// <summary>Resolves a provider by id or alias (case-insensitive), or <c>null</c>.</summary>
    public ISessionProvider? Resolve(string name)
        => _byName.TryGetValue(name.Trim(), out var p) ? p : null;
}
