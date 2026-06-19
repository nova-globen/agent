using AgentSync.Core.Configuration;

namespace AgentSync.Core.Authoring;

/// <summary>Outcome category for a CRUD mutation, mapped to an exit code by the CLI.</summary>
public enum AuthoringStatus
{
    /// <summary>The mutation succeeded (or, for a dry run, would succeed).</summary>
    Ok,

    /// <summary>The target item does not exist (exit 1).</summary>
    NotFound,

    /// <summary>The item already exists / the change conflicts (exit 1).</summary>
    Conflict,

    /// <summary>A destructive change was refused without <c>--force</c> (exit 1).</summary>
    Blocked,

    /// <summary>The resulting state failed validation (exit 1).</summary>
    Invalid,

    /// <summary>Bad arguments: invalid id, unknown target, missing required flag (exit 2).</summary>
    InvalidUsage,

    /// <summary>A path was unsafe / escaped the repository root (exit 3).</summary>
    UnsafePath,
}

/// <summary>
/// The result of a CRUD mutation: a status, the human-readable change lines (paths
/// created/updated/deleted, lockfile entries pruned), any post-mutation validation, and
/// an optional message. Shaped for both human and deterministic JSON rendering.
/// </summary>
public sealed record AuthoringResult(
    AuthoringStatus Status,
    IReadOnlyList<string> Changes,
    IReadOnlyList<ValidationMessage> Validation,
    bool DryRun = false,
    string? Message = null,
    bool RecommendSync = false)
{
    public bool HasValidationErrors => Validation.Any(m => m.Severity == ValidationSeverity.Error);

    public static AuthoringResult Fail(AuthoringStatus status, string message)
        => new(status, Array.Empty<string>(), Array.Empty<ValidationMessage>(), Message: message);
}
