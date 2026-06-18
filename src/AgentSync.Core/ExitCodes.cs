namespace AgentSync.Core;

/// <summary>
/// Process exit codes as defined in <c>.ai-agent/CLI_CONTRACT.md</c>.
/// </summary>
public static class ExitCodes
{
    /// <summary>Command completed successfully.</summary>
    public const int Success = 0;

    /// <summary>Drift was detected or validation failed.</summary>
    public const int DriftOrValidationFailed = 1;

    /// <summary>Invalid command-line usage.</summary>
    public const int InvalidUsage = 2;

    /// <summary>Tool or environment problem (e.g. not a Git repository).</summary>
    public const int EnvironmentProblem = 3;

    /// <summary>Unexpected error.</summary>
    public const int UnexpectedError = 4;
}
