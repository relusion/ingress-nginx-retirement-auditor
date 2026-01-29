using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Infrastructure;

namespace IngressNginxAuditor.Services;

/// <summary>
/// Evaluates scan results against policy thresholds to determine exit codes.
/// </summary>
public class PolicyEvaluator
{
    /// <summary>
    /// Evaluates findings against the policy threshold.
    /// </summary>
    /// <param name="findings">All findings from the scan.</param>
    /// <param name="failOnSeverity">Minimum severity to trigger a failure.</param>
    /// <returns>Policy evaluation result with exit code.</returns>
    public PolicyResult Evaluate(IEnumerable<Finding> findings, Severity failOnSeverity)
    {
        var violationCount = findings.Count(f => f.Severity >= failOnSeverity);

        if (violationCount > 0)
        {
            return PolicyResult.Fail(failOnSeverity, violationCount);
        }

        return PolicyResult.Pass(failOnSeverity);
    }

    /// <summary>
    /// Determines the final exit code based on policy, warnings, and errors.
    /// </summary>
    /// <param name="policy">Policy evaluation result.</param>
    /// <param name="warnings">Warning messages from the scan.</param>
    /// <param name="errors">Error messages from the scan.</param>
    /// <param name="hasFindings">Whether any findings were produced.</param>
    /// <returns>The appropriate exit code.</returns>
    public int DetermineExitCode(
        PolicyResult policy,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors,
        bool hasFindings)
    {
        // Fatal error: errors occurred and no findings
        if (errors.Count > 0 && !hasFindings)
        {
            return ExitCodes.FatalError;
        }

        // Partial failure: errors occurred but some findings were produced
        if (errors.Count > 0 || warnings.Count > 0)
        {
            // If policy also failed, prioritize policy violation
            if (!policy.Passed)
            {
                return ExitCodes.PolicyViolation;
            }

            return ExitCodes.PartialFailure;
        }

        // Policy violation
        if (!policy.Passed)
        {
            return ExitCodes.PolicyViolation;
        }

        // Success
        return ExitCodes.Success;
    }
}
