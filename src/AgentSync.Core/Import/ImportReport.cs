using AgentSync.Core.Configuration;

namespace AgentSync.Core.Import;

/// <summary>What an import did (or would do) to a single canonical skill on disk.</summary>
public enum ImportAction
{
    /// <summary>A new canonical skill would be / was created.</summary>
    Create,

    /// <summary>An existing canonical skill would be / was overwritten (with <c>--force</c>).</summary>
    Overwrite,

    /// <summary>Nothing was written (a conflict without <c>--force</c>, or a malformed source).</summary>
    Skip,
}

/// <summary>The overall outcome of an import run, mapped to an exit code by the CLI.</summary>
public enum ImportStatus
{
    /// <summary>Imported (or previewed) successfully.</summary>
    Ok,

    /// <summary>An imported skill failed validation, or a source was malformed/skipped (exit 1).</summary>
    Problem,

    /// <summary>The source shape is unsupported or an option was invalid (exit 2).</summary>
    InvalidSource,

    /// <summary>The source path does not exist (exit 2).</summary>
    SourceNotFound,

    /// <summary>A path was unsafe / escaped the repository root (exit 3).</summary>
    UnsafePath,
}

/// <summary>One imported (or proposed) canonical skill.</summary>
public sealed record ImportItem(
    string Id,
    string Name,
    string Description,
    string SkillYamlPath,
    string SkillMdPath,
    ImportAction Action,
    IReadOnlyList<ValidationMessage> Validation,
    string SourceRelativePath,
    string? Note = null)
{
    public bool HasValidationErrors => Validation.Any(m => m.Severity == ValidationSeverity.Error);
}

/// <summary>
/// The result of an import: the per-skill items plus an overall <see cref="Status"/>.
/// Shaped like the other service reports so the CLI renders human + JSON output uniformly.
/// </summary>
public sealed record ImportReport(
    ImportStatus Status,
    IReadOnlyList<ImportItem> Items,
    bool DryRun,
    string? Message = null)
{
    public bool AnyWritten => Items.Any(i => i.Action is ImportAction.Create or ImportAction.Overwrite);

    public bool HasValidationErrors => Items.Any(i => i.HasValidationErrors);

    public static ImportReport Failure(ImportStatus status, string message, bool dryRun)
        => new(status, Array.Empty<ImportItem>(), dryRun, message);
}
