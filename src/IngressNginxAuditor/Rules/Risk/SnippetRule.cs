using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Rules;

namespace IngressNginxAuditor.Rules.Risk;

/// <summary>
/// RISK-SNIPPET-001: Detects snippet annotations that inject raw NGINX configuration.
/// These are HIGH/CRITICAL risk as they bypass controller abstractions.
/// </summary>
public class SnippetRule : BaseDetectionRule
{
    // server-snippet is CRITICAL as it can affect the entire server block
    private const string ServerSnippet = "nginx.ingress.kubernetes.io/server-snippet";

    // configuration-snippet and location-snippet are HIGH risk
    private static readonly string[] HighRiskSnippets = new[]
    {
        "nginx.ingress.kubernetes.io/configuration-snippet",
        "nginx.ingress.kubernetes.io/location-snippet"
    };

    private static readonly string[] AllSnippetAnnotations = new[]
    {
        ServerSnippet,
        "nginx.ingress.kubernetes.io/configuration-snippet",
        "nginx.ingress.kubernetes.io/location-snippet",
        "nginx.ingress.kubernetes.io/auth-snippet",
        "nginx.ingress.kubernetes.io/modsecurity-snippet"
    };

    public override string RuleId => "RISK-SNIPPET-001";
    public override string Title => "NGINX snippet annotations detected";
    public override string Category => "MigrationRisk";

    public override Severity DefaultSeverity => Severity.High;
    public override Confidence DefaultConfidence => Confidence.High;

    public override string Description =>
        "Detects Ingress resources using snippet annotations that inject raw NGINX configuration. " +
        "These are high-risk migration blockers as they contain controller-specific configuration " +
        "that must be manually translated to the target controller. Server-snippet is particularly " +
        "dangerous as it can affect the entire server block configuration.";

    public override IReadOnlyList<string> Recommendations => new[]
    {
        "Document the exact NGINX directives used in each snippet",
        "Determine if the functionality can be achieved via standard annotations",
        "Map snippet functionality to Gateway API HTTPRoute policies where possible",
        "Consider using an Envoy filter or Lua script as an alternative",
        "Test thoroughly in non-production before migration"
    };

    protected override bool Matches(NormalizedResource resource)
    {
        if (!resource.Kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
            return false;

        return AllSnippetAnnotations.Any(annotation =>
            resource.Annotations.ContainsKey(annotation));
    }

    protected override Evidence CollectEvidence(NormalizedResource resource, bool showAnnotationValues)
    {
        var matchingAnnotations = resource.Annotations
            .Where(kvp => AllSnippetAnnotations.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                kvp => kvp.Key,
                kvp => RedactedAnnotation.FromAnnotation(kvp.Key, kvp.Value, showAnnotationValues));

        var matchedPatterns = matchingAnnotations.Keys.ToList();

        return new Evidence
        {
            Annotations = matchingAnnotations,
            MatchedPatterns = matchedPatterns
        };
    }

    /// <summary>
    /// Determines the effective severity based on which snippets are present.
    /// server-snippet = CRITICAL, others = HIGH
    /// </summary>
    protected override Severity GetEffectiveSeverity(NormalizedResource resource)
    {
        // Check for config override first
        if (EffectiveSeverity != default)
            return EffectiveSeverity;

        if (resource.Annotations.ContainsKey(ServerSnippet))
            return Severity.Critical;

        if (HighRiskSnippets.Any(s => resource.Annotations.ContainsKey(s)))
            return Severity.High;

        return Severity.High; // Default for other snippets
    }

    protected override string FormatMessage(NormalizedResource resource)
    {
        var snippetTypes = AllSnippetAnnotations
            .Where(a => resource.Annotations.ContainsKey(a))
            .Select(a => a.Split('/').Last())
            .ToList();

        var severity = resource.Annotations.ContainsKey(ServerSnippet) ? "CRITICAL" : "HIGH";

        return $"Ingress '{resource}' uses {snippetTypes.Count} snippet annotation(s): " +
               $"[{string.Join(", ", snippetTypes)}] - {severity} migration risk";
    }
}
