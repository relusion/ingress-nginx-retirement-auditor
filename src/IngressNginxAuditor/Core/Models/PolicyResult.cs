using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Result of policy evaluation.
/// </summary>
public sealed record PolicyResult
{
    /// <summary>Whether the policy check passed.</summary>
    public required bool Passed { get; init; }

    /// <summary>The configured fail-on severity threshold.</summary>
    public required Severity FailOnSeverity { get; init; }

    /// <summary>Count of findings at or above the threshold.</summary>
    public required int ViolationCount { get; init; }

    /// <summary>Recommended exit code based on policy evaluation.</summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Creates a passing policy result.
    /// </summary>
    public static PolicyResult Pass(Severity failOnSeverity) => new()
    {
        Passed = true,
        FailOnSeverity = failOnSeverity,
        ViolationCount = 0,
        ExitCode = 0
    };

    /// <summary>
    /// Creates a failing policy result.
    /// </summary>
    public static PolicyResult Fail(Severity failOnSeverity, int violationCount) => new()
    {
        Passed = false,
        FailOnSeverity = failOnSeverity,
        ViolationCount = violationCount,
        ExitCode = 1
    };
}
