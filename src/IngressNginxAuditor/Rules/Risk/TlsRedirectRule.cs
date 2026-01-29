using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Rules;

namespace IngressNginxAuditor.Rules.Risk;

/// <summary>
/// RISK-TLS-REDIRECT-001: Detects SSL/TLS redirect annotations.
/// ssl-redirect is LOW risk, force-ssl-redirect is MEDIUM risk.
/// </summary>
public class TlsRedirectRule : BaseDetectionRule
{
    private const string SslRedirectAnnotation = "nginx.ingress.kubernetes.io/ssl-redirect";
    private const string ForceSslRedirectAnnotation = "nginx.ingress.kubernetes.io/force-ssl-redirect";
    private const string TemporalRedirectAnnotation = "nginx.ingress.kubernetes.io/temporal-redirect";
    private const string PermanentRedirectAnnotation = "nginx.ingress.kubernetes.io/permanent-redirect";

    private static readonly string[] RedirectAnnotations = new[]
    {
        SslRedirectAnnotation,
        ForceSslRedirectAnnotation,
        TemporalRedirectAnnotation,
        PermanentRedirectAnnotation
    };

    public override string RuleId => "RISK-TLS-REDIRECT-001";
    public override string Title => "TLS/SSL redirect configuration detected";
    public override string Category => "MigrationRisk";

    // Default to Low, but force-ssl-redirect is Medium
    public override Severity DefaultSeverity => Severity.Low;
    public override Confidence DefaultConfidence => Confidence.High;

    public override string Description =>
        "Detects Ingress resources with SSL/TLS redirect annotations. While ssl-redirect is " +
        "commonly supported, force-ssl-redirect and other redirect patterns may need " +
        "specific configuration in the target controller.";

    public override IReadOnlyList<string> Recommendations => new[]
    {
        "Verify TLS redirect behavior is supported by target controller",
        "Most controllers support automatic HTTPS redirect",
        "Gateway API supports redirect via HTTPRoute filters",
        "Test redirect behavior with both HTTP and HTTPS clients"
    };

    protected override bool Matches(NormalizedResource resource)
    {
        if (!resource.Kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
            return false;

        return RedirectAnnotations.Any(annotation =>
        {
            if (resource.Annotations.TryGetValue(annotation, out var value))
            {
                // Only match if value is "true" for boolean annotations
                if (annotation == SslRedirectAnnotation || annotation == ForceSslRedirectAnnotation)
                {
                    return value.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
                // For redirect annotations, any non-empty value matches
                return !string.IsNullOrEmpty(value);
            }
            return false;
        });
    }

    protected override Evidence CollectEvidence(NormalizedResource resource, bool showAnnotationValues)
    {
        var matchingAnnotations = resource.Annotations
            .Where(kvp => RedirectAnnotations.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                kvp => kvp.Key,
                kvp => RedactedAnnotation.FromAnnotation(kvp.Key, kvp.Value, showAnnotationValues));

        var matchedPatterns = matchingAnnotations
            .Select(kvp => $"{kvp.Key.Split('/').Last()}={kvp.Value.TruncatedValue ?? "***"}")
            .ToList();

        return new Evidence
        {
            Annotations = matchingAnnotations,
            MatchedPatterns = matchedPatterns
        };
    }

    /// <summary>
    /// Determines the effective severity based on which redirect type is used.
    /// force-ssl-redirect = MEDIUM, others = LOW
    /// </summary>
    public Severity DetermineSeverity(NormalizedResource resource)
    {
        if (resource.Annotations.TryGetValue(ForceSslRedirectAnnotation, out var forceValue) &&
            forceValue.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return Severity.Medium;
        }

        if (resource.Annotations.ContainsKey(TemporalRedirectAnnotation) ||
            resource.Annotations.ContainsKey(PermanentRedirectAnnotation))
        {
            return Severity.Medium;
        }

        return Severity.Low;
    }

    protected override string FormatMessage(NormalizedResource resource)
    {
        var redirectTypes = new List<string>();

        if (resource.Annotations.TryGetValue(SslRedirectAnnotation, out var sslValue) &&
            sslValue.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            redirectTypes.Add("ssl-redirect");
        }

        if (resource.Annotations.TryGetValue(ForceSslRedirectAnnotation, out var forceValue) &&
            forceValue.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            redirectTypes.Add("force-ssl-redirect");
        }

        if (resource.Annotations.ContainsKey(TemporalRedirectAnnotation))
            redirectTypes.Add("temporal-redirect");

        if (resource.Annotations.ContainsKey(PermanentRedirectAnnotation))
            redirectTypes.Add("permanent-redirect");

        return $"Ingress '{resource}' uses redirect configuration: [{string.Join(", ", redirectTypes)}]";
    }
}
