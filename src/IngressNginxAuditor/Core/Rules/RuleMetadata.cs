using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Rules;

/// <summary>
/// Metadata about a detection rule for display and documentation.
/// </summary>
public sealed record RuleMetadata
{
    /// <summary>Unique rule identifier.</summary>
    public required string RuleId { get; init; }

    /// <summary>Human-readable title.</summary>
    public required string Title { get; init; }

    /// <summary>Rule category.</summary>
    public required string Category { get; init; }

    /// <summary>Default severity level.</summary>
    public required Severity DefaultSeverity { get; init; }

    /// <summary>Default confidence level.</summary>
    public required Confidence DefaultConfidence { get; init; }

    /// <summary>Detailed description.</summary>
    public required string Description { get; init; }

    /// <summary>Why this rule matters for migration.</summary>
    public string? Rationale { get; init; }

    /// <summary>Classification tags for filtering and grouping.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Recommended actions.</summary>
    public required IReadOnlyList<string> Recommendations { get; init; }

    /// <summary>External documentation references.</summary>
    public IReadOnlyList<string> References { get; init; } = [];
}
