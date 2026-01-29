namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Evidence collected during rule evaluation.
/// Contains the specific data that triggered the finding.
/// </summary>
public sealed record Evidence
{
    /// <summary>Redacted annotations that matched the rule (if applicable).</summary>
    public IReadOnlyDictionary<string, RedactedAnnotation>? Annotations { get; init; }

    /// <summary>Labels that matched the rule (if applicable).</summary>
    public IReadOnlyDictionary<string, string>? Labels { get; init; }

    /// <summary>IngressClassName that matched (if applicable).</summary>
    public string? IngressClassName { get; init; }

    /// <summary>Patterns or values that matched the rule.</summary>
    public IReadOnlyList<string>? MatchedPatterns { get; init; }

    /// <summary>
    /// Creates an empty evidence object.
    /// </summary>
    public static Evidence Empty => new();

    /// <summary>
    /// Creates evidence with matched annotations.
    /// </summary>
    public static Evidence FromAnnotations(IReadOnlyDictionary<string, RedactedAnnotation> annotations) =>
        new() { Annotations = annotations };

    /// <summary>
    /// Creates evidence with matched labels.
    /// </summary>
    public static Evidence FromLabels(IReadOnlyDictionary<string, string> labels) =>
        new() { Labels = labels };

    /// <summary>
    /// Creates evidence with a matched IngressClassName.
    /// </summary>
    public static Evidence FromIngressClassName(string ingressClassName) =>
        new() { IngressClassName = ingressClassName };

    /// <summary>
    /// Creates evidence with matched patterns.
    /// </summary>
    public static Evidence FromPatterns(IReadOnlyList<string> patterns) =>
        new() { MatchedPatterns = patterns };
}
