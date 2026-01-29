using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Rules;

/// <summary>
/// Interface for detection and risk rules.
/// Rules evaluate resources and emit findings when conditions are met.
/// </summary>
public interface IDetectionRule
{
    /// <summary>Unique rule identifier (e.g., "RISK-SNIPPET-001").</summary>
    string RuleId { get; }

    /// <summary>Human-readable title.</summary>
    string Title { get; }

    /// <summary>Rule category (e.g., "Detection", "MigrationRisk").</summary>
    string Category { get; }

    /// <summary>Default severity level for findings from this rule.</summary>
    Severity DefaultSeverity { get; }

    /// <summary>Default confidence level for findings from this rule.</summary>
    Confidence DefaultConfidence { get; }

    /// <summary>Detailed description of what this rule detects.</summary>
    string Description { get; }

    /// <summary>Recommended actions when this rule triggers.</summary>
    IReadOnlyList<string> Recommendations { get; }

    /// <summary>
    /// Evaluates a resource against this rule.
    /// </summary>
    /// <param name="resource">The normalized resource to evaluate.</param>
    /// <param name="showAnnotationValues">Whether to include annotation value previews in evidence.</param>
    /// <returns>A finding if the rule matches, or null if it doesn't.</returns>
    Finding? Evaluate(NormalizedResource resource, bool showAnnotationValues = false);

    /// <summary>
    /// Gets metadata for this rule.
    /// </summary>
    RuleMetadata GetMetadata();
}
