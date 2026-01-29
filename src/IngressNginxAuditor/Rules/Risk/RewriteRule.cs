using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Rules;

namespace IngressNginxAuditor.Rules.Risk;

/// <summary>
/// RISK-REWRITE-001: Detects rewrite-target annotation for URL rewriting.
/// This is MEDIUM risk as rewrite patterns need translation.
/// </summary>
public class RewriteRule : BaseDetectionRule
{
    private const string RewriteTargetAnnotation = "nginx.ingress.kubernetes.io/rewrite-target";
    private const string AppRootAnnotation = "nginx.ingress.kubernetes.io/app-root";

    private static readonly string[] RewriteRelatedAnnotations = new[]
    {
        RewriteTargetAnnotation,
        AppRootAnnotation,
        "nginx.ingress.kubernetes.io/x-forwarded-prefix"
    };

    public override string RuleId => "RISK-REWRITE-001";
    public override string Title => "URL rewrite patterns detected";
    public override string Category => "MigrationRisk";
    public override Severity DefaultSeverity => Severity.Medium;
    public override Confidence DefaultConfidence => Confidence.High;

    public override string Description =>
        "Detects Ingress resources using rewrite-target annotation for URL path rewriting. " +
        "Rewrite patterns, especially those using regex capture groups, require careful " +
        "translation when migrating to other controllers.";

    public override IReadOnlyList<string> Recommendations => new[]
    {
        "Document all rewrite patterns and their intended behavior",
        "Map rewrite-target to equivalent mechanisms in target controller",
        "Gateway API HTTPRoute supports URLRewrite filter for path modification",
        "Test rewrite behavior thoroughly after migration"
    };

    protected override bool Matches(NormalizedResource resource)
    {
        if (!resource.Kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
            return false;

        return RewriteRelatedAnnotations.Any(annotation =>
            resource.Annotations.ContainsKey(annotation));
    }

    protected override Evidence CollectEvidence(NormalizedResource resource, bool showAnnotationValues)
    {
        var matchingAnnotations = resource.Annotations
            .Where(kvp => RewriteRelatedAnnotations.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                kvp => kvp.Key,
                kvp => RedactedAnnotation.FromAnnotation(kvp.Key, kvp.Value, showAnnotationValues));

        var matchedPatterns = matchingAnnotations.Keys
            .Select(k => k.Split('/').Last())
            .ToList();

        return new Evidence
        {
            Annotations = matchingAnnotations,
            MatchedPatterns = matchedPatterns
        };
    }

    protected override string FormatMessage(NormalizedResource resource)
    {
        var hasRegexCapture = resource.Annotations.TryGetValue(RewriteTargetAnnotation, out var target) &&
                              target.Contains("$");

        var riskNote = hasRegexCapture
            ? "uses regex capture groups - requires careful translation"
            : "needs migration to target controller's rewrite mechanism";

        return $"Ingress '{resource}' uses URL rewriting - {riskNote}";
    }
}
