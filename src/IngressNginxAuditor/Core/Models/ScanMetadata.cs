using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Metadata about the scan execution.
/// </summary>
public sealed record ScanMetadata
{
    /// <summary>Name of the tool that performed the scan.</summary>
    public required string Tool { get; init; }

    /// <summary>Version of the tool.</summary>
    public required string Version { get; init; }

    /// <summary>Scan mode (Cluster or Repo).</summary>
    public required ScanMode Mode { get; init; }

    /// <summary>UTC timestamp when the scan was performed.</summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>JSON schema version for output format.</summary>
    public required string SchemaVersion { get; init; }

    /// <summary>Status of the scan (complete or incomplete).</summary>
    public required string Status { get; init; }

    /// <summary>Cluster context name (for cluster mode).</summary>
    public string? ClusterContext { get; init; }

    /// <summary>Scanned path (for repo mode).</summary>
    public string? ScannedPath { get; init; }

    /// <summary>Duration of the scan.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Creates metadata for a cluster scan.
    /// </summary>
    public static ScanMetadata ForClusterScan(
        string toolVersion,
        string schemaVersion,
        string? context = null,
        string status = "complete") => new()
    {
        Tool = "ingress-nginx-auditor",
        Version = toolVersion,
        Mode = ScanMode.Cluster,
        TimestampUtc = DateTimeOffset.UtcNow,
        SchemaVersion = schemaVersion,
        Status = status,
        ClusterContext = context
    };

    /// <summary>
    /// Creates metadata for a repo scan.
    /// </summary>
    public static ScanMetadata ForRepoScan(
        string toolVersion,
        string schemaVersion,
        string? scannedPath = null,
        string status = "complete") => new()
    {
        Tool = "ingress-nginx-auditor",
        Version = toolVersion,
        Mode = ScanMode.Repo,
        TimestampUtc = DateTimeOffset.UtcNow,
        SchemaVersion = schemaVersion,
        Status = status,
        ScannedPath = scannedPath
    };
}
