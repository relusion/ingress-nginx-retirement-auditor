using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Summary statistics for a completed scan.
/// </summary>
public sealed record ScanSummary
{
    /// <summary>Total number of Kubernetes resources scanned.</summary>
    public required int ResourcesScanned { get; init; }

    /// <summary>Number of Ingress resources scanned.</summary>
    public required int IngressesScanned { get; init; }

    /// <summary>Number of Ingresses identified as using ingress-nginx.</summary>
    public required int NginxDependentIngresses { get; init; }

    /// <summary>Total findings count by severity level.</summary>
    public required IReadOnlyDictionary<Severity, int> FindingsBySeverity { get; init; }

    /// <summary>Rollup data by namespace.</summary>
    public required IReadOnlyDictionary<string, NamespaceRollup> ByNamespace { get; init; }

    /// <summary>Policy evaluation result.</summary>
    public required PolicyResult Policy { get; init; }

    /// <summary>Total number of findings.</summary>
    public int TotalFindings => FindingsBySeverity.Values.Sum();

    /// <summary>
    /// Creates an empty summary with zero counts.
    /// </summary>
    public static ScanSummary Empty(PolicyResult policy) => new()
    {
        ResourcesScanned = 0,
        IngressesScanned = 0,
        NginxDependentIngresses = 0,
        FindingsBySeverity = CreateEmptySeverityCounts(),
        ByNamespace = new Dictionary<string, NamespaceRollup>(),
        Policy = policy
    };

    /// <summary>
    /// Creates a dictionary with zero counts for all severity levels.
    /// </summary>
    public static IReadOnlyDictionary<Severity, int> CreateEmptySeverityCounts() =>
        Enum.GetValues<Severity>().ToDictionary(s => s, _ => 0);
}
