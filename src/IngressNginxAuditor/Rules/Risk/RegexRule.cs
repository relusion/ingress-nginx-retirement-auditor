using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Rules;

namespace IngressNginxAuditor.Rules.Risk;

/// <summary>
/// RISK-REGEX-001: Detects use-regex annotation indicating regex path matching.
/// This is MEDIUM risk as regex paths need careful translation.
/// </summary>
public class RegexRule : BaseDetectionRule
{
    private const string UseRegexAnnotation = "nginx.ingress.kubernetes.io/use-regex";

    public override string RuleId => "RISK-REGEX-001";
    public override string Title => "Regex path matching enabled";
    public override string Category => "MigrationRisk";
    public override Severity DefaultSeverity => Severity.Medium;
    public override Confidence DefaultConfidence => Confidence.High;

    public override string Description =>
        "Detects Ingress resources with use-regex annotation set to 'true', enabling " +
        "regex-based path matching. Regex patterns may need modification when migrating " +
        "to controllers with different regex engines or path matching semantics.";

    public override IReadOnlyList<string> Recommendations => new[]
    {
        "Document all regex patterns used in Ingress paths",
        "Test regex patterns against target controller's regex engine",
        "Consider simplifying patterns to prefix or exact match where possible",
        "Gateway API HTTPRoute supports regex matching in its PathMatch"
    };

    protected override bool Matches(NormalizedResource resource)
    {
        if (!resource.Kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
            return false;

        if (resource.Annotations.TryGetValue(UseRegexAnnotation, out var value))
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    protected override Evidence CollectEvidence(NormalizedResource resource, bool showAnnotationValues)
    {
        var annotations = new Dictionary<string, RedactedAnnotation>();

        if (resource.Annotations.TryGetValue(UseRegexAnnotation, out var value))
        {
            annotations[UseRegexAnnotation] = RedactedAnnotation.FromAnnotation(
                UseRegexAnnotation, value, showAnnotationValues);
        }

        return new Evidence
        {
            Annotations = annotations,
            MatchedPatterns = new[] { $"{UseRegexAnnotation}=true" }
        };
    }

    protected override string FormatMessage(NormalizedResource resource) =>
        $"Ingress '{resource}' uses regex path matching - verify patterns work with target controller";
}
