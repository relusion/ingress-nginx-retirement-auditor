using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Scoring;

/// <summary>
/// Defines the weights used for risk score calculation.
/// Risk score = sum of weighted severities, capped at 100.
/// </summary>
public static class SeverityWeights
{
    /// <summary>Weight for CRITICAL severity findings.</summary>
    public const int Critical = 40;

    /// <summary>Weight for HIGH severity findings.</summary>
    public const int High = 25;

    /// <summary>Weight for MEDIUM severity findings.</summary>
    public const int Medium = 10;

    /// <summary>Weight for LOW severity findings.</summary>
    public const int Low = 5;

    /// <summary>Weight for INFO severity findings.</summary>
    public const int Info = 1;

    /// <summary>Maximum risk score (cap).</summary>
    public const int MaxScore = 100;

    /// <summary>
    /// Gets the weight for a given severity level.
    /// </summary>
    public static int GetWeight(Severity severity) => severity switch
    {
        Severity.Critical => Critical,
        Severity.High => High,
        Severity.Medium => Medium,
        Severity.Low => Low,
        Severity.Info => Info,
        _ => 0
    };
}
