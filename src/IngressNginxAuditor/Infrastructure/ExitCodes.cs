namespace IngressNginxAuditor.Infrastructure;

/// <summary>
/// Standard exit codes for the CLI tool.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Success - completed with no policy violations.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Policy violation - findings at or above fail-on threshold.
    /// </summary>
    public const int PolicyViolation = 1;

    /// <summary>
    /// Invalid configuration or arguments.
    /// </summary>
    public const int InvalidConfig = 2;

    /// <summary>
    /// Partial failure - some errors occurred but results were produced.
    /// </summary>
    public const int PartialFailure = 3;

    /// <summary>
    /// Fatal error - could not complete the scan.
    /// </summary>
    public const int FatalError = 4;

    /// <summary>
    /// Gets the description for an exit code.
    /// </summary>
    public static string GetDescription(int exitCode) => exitCode switch
    {
        Success => "Success - no policy violations",
        PolicyViolation => "Policy violation - findings exceed threshold",
        InvalidConfig => "Invalid configuration or arguments",
        PartialFailure => "Partial failure - check warnings",
        FatalError => "Fatal error - check connectivity/permissions",
        _ => $"Unknown exit code: {exitCode}"
    };
}
