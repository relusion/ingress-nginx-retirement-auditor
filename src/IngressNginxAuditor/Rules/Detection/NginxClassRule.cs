using IngressNginxAuditor.Configuration;
using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Rules;

namespace IngressNginxAuditor.Rules.Detection;

/// <summary>
/// DET-NGINX-CLASS-001: Detects Ingress resources with ingressClassName or legacy annotation
/// matching configured NGINX class values.
/// </summary>
public class NginxClassRule : BaseDetectionRule
{
    private readonly IReadOnlyList<string> _ingressClassNames;

    public NginxClassRule() : this(DefaultConfiguration.IngressClassNames)
    {
    }

    public NginxClassRule(IReadOnlyList<string> ingressClassNames)
    {
        _ingressClassNames = ingressClassNames;
    }

    public override string RuleId => "DET-NGINX-CLASS-001";
    public override string Title => "NGINX IngressClass detected";
    public override string Category => "Detection";
    public override Severity DefaultSeverity => Severity.Info;
    public override Confidence DefaultConfidence => Confidence.High;

    public override string Description =>
        "Detects Ingress resources configured to use the ingress-nginx controller via " +
        "spec.ingressClassName or the legacy kubernetes.io/ingress.class annotation.";

    public override IReadOnlyList<string> Recommendations => new[]
    {
        "Plan migration to an alternative Ingress controller (e.g., Envoy Gateway, Istio, Traefik)",
        "Review the ingress-nginx retirement timeline and plan accordingly",
        "Consider using Gateway API as a future-proof alternative"
    };

    protected override bool Matches(NormalizedResource resource)
    {
        if (!resource.Kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
            return false;

        // Check spec.ingressClassName
        if (!string.IsNullOrEmpty(resource.IngressClassName))
        {
            if (_ingressClassNames.Any(cn =>
                cn.Equals(resource.IngressClassName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Check legacy annotation
        if (resource.Annotations.TryGetValue(DefaultConfiguration.LegacyIngressClassAnnotation, out var legacyClass))
        {
            if (_ingressClassNames.Any(cn =>
                cn.Equals(legacyClass, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    protected override Evidence CollectEvidence(NormalizedResource resource, bool showAnnotationValues)
    {
        var matchedPatterns = new List<string>();

        if (!string.IsNullOrEmpty(resource.IngressClassName) &&
            _ingressClassNames.Any(cn => cn.Equals(resource.IngressClassName, StringComparison.OrdinalIgnoreCase)))
        {
            matchedPatterns.Add($"spec.ingressClassName: {resource.IngressClassName}");
        }

        if (resource.Annotations.TryGetValue(DefaultConfiguration.LegacyIngressClassAnnotation, out var legacyClass) &&
            _ingressClassNames.Any(cn => cn.Equals(legacyClass, StringComparison.OrdinalIgnoreCase)))
        {
            matchedPatterns.Add($"annotation {DefaultConfiguration.LegacyIngressClassAnnotation}: {legacyClass}");
        }

        return new Evidence
        {
            IngressClassName = resource.IngressClassName,
            MatchedPatterns = matchedPatterns
        };
    }

    protected override string FormatMessage(NormalizedResource resource)
    {
        var source = !string.IsNullOrEmpty(resource.IngressClassName)
            ? $"ingressClassName '{resource.IngressClassName}'"
            : "legacy annotation";

        return $"Ingress '{resource}' uses ingress-nginx controller via {source}";
    }
}
