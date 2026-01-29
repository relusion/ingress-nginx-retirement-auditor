using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Rules;

namespace IngressNginxAuditor.Rules.Risk;

/// <summary>
/// RISK-AUTH-001: Detects external authentication annotations.
/// This is MEDIUM risk as auth configuration needs careful migration.
/// </summary>
public class AuthRule : BaseDetectionRule
{
    // External authentication annotations
    private static readonly string[] AuthAnnotations = new[]
    {
        "nginx.ingress.kubernetes.io/auth-url",
        "nginx.ingress.kubernetes.io/auth-signin",
        "nginx.ingress.kubernetes.io/auth-signin-redirect-param",
        "nginx.ingress.kubernetes.io/auth-method",
        "nginx.ingress.kubernetes.io/auth-response-headers",
        "nginx.ingress.kubernetes.io/auth-request-redirect",
        "nginx.ingress.kubernetes.io/auth-cache-key",
        "nginx.ingress.kubernetes.io/auth-cache-duration",
        "nginx.ingress.kubernetes.io/auth-always-set-cookie"
    };

    // Basic auth annotations
    private static readonly string[] BasicAuthAnnotations = new[]
    {
        "nginx.ingress.kubernetes.io/auth-type",
        "nginx.ingress.kubernetes.io/auth-secret",
        "nginx.ingress.kubernetes.io/auth-secret-type",
        "nginx.ingress.kubernetes.io/auth-realm"
    };

    private static readonly string[] AllAuthAnnotations =
        AuthAnnotations.Concat(BasicAuthAnnotations).ToArray();

    public override string RuleId => "RISK-AUTH-001";
    public override string Title => "External authentication configuration detected";
    public override string Category => "MigrationRisk";
    public override Severity DefaultSeverity => Severity.Medium;
    public override Confidence DefaultConfidence => Confidence.High;

    public override string Description =>
        "Detects Ingress resources configured with external authentication (auth-url, auth-signin) " +
        "or basic authentication (auth-type, auth-secret). These configurations integrate with " +
        "identity providers or secrets and require careful migration to maintain security.";

    public override IReadOnlyList<string> Recommendations => new[]
    {
        "Document the authentication flow and identity provider configuration",
        "Verify target controller supports equivalent auth mechanisms",
        "Consider migrating to Gateway API with ExtAuth extension",
        "Test authentication thoroughly in staging before production migration",
        "Ensure secrets are properly migrated if using basic auth"
    };

    protected override bool Matches(NormalizedResource resource)
    {
        if (!resource.Kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
            return false;

        return AllAuthAnnotations.Any(annotation =>
            resource.Annotations.ContainsKey(annotation));
    }

    protected override Evidence CollectEvidence(NormalizedResource resource, bool showAnnotationValues)
    {
        var matchingAnnotations = resource.Annotations
            .Where(kvp => AllAuthAnnotations.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                kvp => kvp.Key,
                kvp => RedactedAnnotation.FromAnnotation(kvp.Key, kvp.Value, showAnnotationValues));

        var authType = DetermineAuthType(resource);
        var matchedPatterns = new List<string> { $"Authentication type: {authType}" };
        matchedPatterns.AddRange(matchingAnnotations.Keys.Select(k => k.Split('/').Last()));

        return new Evidence
        {
            Annotations = matchingAnnotations,
            MatchedPatterns = matchedPatterns
        };
    }

    protected override string FormatMessage(NormalizedResource resource)
    {
        var authType = DetermineAuthType(resource);
        var annotationCount = AllAuthAnnotations.Count(a => resource.Annotations.ContainsKey(a));

        return $"Ingress '{resource}' uses {authType} authentication ({annotationCount} annotation(s))";
    }

    private static string DetermineAuthType(NormalizedResource resource)
    {
        if (resource.Annotations.ContainsKey("nginx.ingress.kubernetes.io/auth-url"))
            return "external";

        if (resource.Annotations.TryGetValue("nginx.ingress.kubernetes.io/auth-type", out var authType))
            return authType.ToLowerInvariant();

        if (resource.Annotations.ContainsKey("nginx.ingress.kubernetes.io/auth-secret"))
            return "basic";

        return "unknown";
    }
}
