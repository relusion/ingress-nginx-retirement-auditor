using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Represents a detection or risk finding for a resource.
/// </summary>
public sealed record Finding
{
    /// <summary>Unique rule identifier (e.g., "RISK-SNIPPET-001").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable title of the finding.</summary>
    public required string Title { get; init; }

    /// <summary>Severity level of the finding.</summary>
    public required Severity Severity { get; init; }

    /// <summary>Confidence level of the detection.</summary>
    public required Confidence Confidence { get; init; }

    /// <summary>Category of the finding (e.g., "Detection", "MigrationRisk").</summary>
    public required string Category { get; init; }

    /// <summary>Reference to the resource that triggered the finding.</summary>
    public required ResourceReference Resource { get; init; }

    /// <summary>Evidence that triggered the finding.</summary>
    public required Evidence Evidence { get; init; }

    /// <summary>Detailed message describing the finding.</summary>
    public required string Message { get; init; }

    /// <summary>Recommended actions to address the finding.</summary>
    public required IReadOnlyList<string> Recommendations { get; init; }
}
