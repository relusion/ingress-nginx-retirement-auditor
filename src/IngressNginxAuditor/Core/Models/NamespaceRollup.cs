using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Aggregated findings for a single namespace.
/// </summary>
public sealed record NamespaceRollup
{
    /// <summary>The namespace name.</summary>
    public required string Namespace { get; init; }

    /// <summary>Number of Ingress resources in this namespace.</summary>
    public required int IngressCount { get; init; }

    /// <summary>Number of findings in this namespace.</summary>
    public required int FindingCount { get; init; }

    /// <summary>Findings count by severity level.</summary>
    public required IReadOnlyDictionary<Severity, int> BySeverity { get; init; }

    /// <summary>Maximum risk score among all Ingresses in this namespace.</summary>
    public required int MaxRiskScore { get; init; }
}
