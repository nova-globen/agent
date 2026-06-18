using AgentSync.Core.Configuration;

namespace AgentSync.Core.Adapters;

/// <summary>How a projection is written to its target file.</summary>
public enum ProjectionMode
{
    /// <summary>A managed section inside a possibly-shared file (uses agent-sync markers).</summary>
    SharedSection,

    /// <summary>A dedicated file whose entire contents are generated.</summary>
    WholeFile,
}

/// <summary>A single planned projection: one skill rendered for one target.</summary>
public sealed record Projection(
    string SkillId,
    string TargetId,
    ProjectionMode Mode,
    string RelativePath,
    string Body);

/// <summary>Converts a canonical skill into target-specific content and resolves its path.</summary>
public interface ISkillAdapter
{
    /// <summary>The target id this adapter handles (see <see cref="TargetIds"/>).</summary>
    string TargetId { get; }

    /// <summary>Whether output is a managed section or a whole generated file.</summary>
    ProjectionMode Mode { get; }

    /// <summary>Resolves the projection's repo-relative path from the configured target path.</summary>
    string ResolvePath(string configuredPath, Skill skill);

    /// <summary>Renders the deterministic content for the skill.</summary>
    string Render(Skill skill);
}
