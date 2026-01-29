using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Scoring;

/// <summary>
/// Calculates risk scores for resources based on their findings.
/// </summary>
public class RiskScorer
{
    /// <summary>
    /// Calculates the risk score for a set of findings.
    /// Score is the sum of severity weights, capped at 100.
    /// </summary>
    /// <param name="findings">The findings to calculate score for.</param>
    /// <returns>Risk score from 0 to 100.</returns>
    public int CalculateScore(IEnumerable<Finding> findings)
    {
        var totalWeight = findings.Sum(f => SeverityWeights.GetWeight(f.Severity));
        return Math.Min(totalWeight, SeverityWeights.MaxScore);
    }

    /// <summary>
    /// Calculates the risk score for a set of findings with confidence modifier.
    /// </summary>
    /// <param name="findings">The findings to calculate score for.</param>
    /// <param name="applyConfidenceModifier">Whether to apply confidence modifiers.</param>
    /// <returns>Risk score from 0 to 100.</returns>
    public int CalculateScore(IEnumerable<Finding> findings, bool applyConfidenceModifier)
    {
        if (!applyConfidenceModifier)
            return CalculateScore(findings);

        var totalWeight = findings.Sum(f =>
        {
            var weight = SeverityWeights.GetWeight(f.Severity);
            var modifier = GetConfidenceModifier(f.Confidence);
            return (int)(weight * modifier);
        });

        return Math.Min(totalWeight, SeverityWeights.MaxScore);
    }

    /// <summary>
    /// Gets the confidence modifier multiplier.
    /// </summary>
    private static double GetConfidenceModifier(Confidence confidence) => confidence switch
    {
        Confidence.High => 1.0,
        Confidence.Medium => 0.75,
        Confidence.Low => 0.5,
        _ => 1.0
    };

    /// <summary>
    /// Determines the risk level category based on score.
    /// </summary>
    public static string GetRiskLevel(int score) => score switch
    {
        >= 80 => "Critical",
        >= 50 => "High",
        >= 25 => "Medium",
        >= 10 => "Low",
        _ => "Minimal"
    };
}
