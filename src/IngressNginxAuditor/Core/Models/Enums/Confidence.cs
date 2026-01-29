namespace IngressNginxAuditor.Core.Models.Enums;

/// <summary>
/// Confidence level for detection findings.
/// Indicates certainty that the resource uses ingress-nginx.
/// </summary>
public enum Confidence
{
    /// <summary>Low confidence detection (e.g., annotation prefix match only).</summary>
    Low = 0,

    /// <summary>Medium confidence detection (e.g., class name match).</summary>
    Medium = 1,

    /// <summary>High confidence detection (e.g., explicit controller reference).</summary>
    High = 2
}
