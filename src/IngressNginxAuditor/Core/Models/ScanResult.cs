namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Complete result of a scan operation.
/// </summary>
public sealed record ScanResult
{
    /// <summary>Metadata about the scan execution.</summary>
    public required ScanMetadata Metadata { get; init; }

    /// <summary>Summary statistics.</summary>
    public required ScanSummary Summary { get; init; }

    /// <summary>All findings from the scan.</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>Warning messages (non-fatal issues during scan).</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Error messages (failures during scan).</summary>
    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// Creates an empty result with no findings.
    /// </summary>
    public static ScanResult Empty(ScanMetadata metadata, PolicyResult policy) => new()
    {
        Metadata = metadata,
        Summary = ScanSummary.Empty(policy),
        Findings = [],
        Warnings = [],
        Errors = []
    };
}
