using IngressNginxAuditor.Configuration;
using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Rules;

namespace IngressNginxAuditor.Rules.Detection;

/// <summary>
/// DET-NGINX-ANNOT-PREFIX-001: Detects Ingress resources with annotations using
/// nginx.ingress.kubernetes.io/* or nginx.org/* prefixes.
/// </summary>
public class AnnotationPrefixRule : BaseDetectionRule
{
    private readonly IReadOnlyList<string> _annotationPrefixes;

    public AnnotationPrefixRule() : this(DefaultConfiguration.AnnotationPrefixes)
    {
    }

    public AnnotationPrefixRule(IReadOnlyList<string> annotationPrefixes)
    {
        _annotationPrefixes = annotationPrefixes;
    }

    public override string RuleId => "DET-NGINX-ANNOT-PREFIX-001";
    public override string Title => "NGINX-specific annotations detected";
    public override string Category => "Detection";
    public override Severity DefaultSeverity => Severity.Info;
    public override Confidence DefaultConfidence => Confidence.High;

    public override string Description =>
        "Detects Ingress resources with annotations using ingress-nginx specific prefixes " +
        "(nginx.ingress.kubernetes.io/* or nginx.org/*). These annotations configure NGINX-specific " +
        "behavior that will need migration.";

    public override IReadOnlyList<string> Recommendations => new[]
    {
        "Document all NGINX-specific annotations in use",
        "Map annotation functionality to equivalent features in target controller",
        "Test annotation migration in a non-production environment first"
    };

    protected override bool Matches(NormalizedResource resource)
    {
        if (!resource.Kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
            return false;

        return resource.Annotations.Keys.Any(key =>
            _annotationPrefixes.Any(prefix =>
                key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
    }

    protected override Evidence CollectEvidence(NormalizedResource resource, bool showAnnotationValues)
    {
        var matchingAnnotations = resource.Annotations
            .Where(kvp => _annotationPrefixes.Any(prefix =>
                kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(
                kvp => kvp.Key,
                kvp => RedactedAnnotation.FromAnnotation(kvp.Key, kvp.Value, showAnnotationValues));

        return Evidence.FromAnnotations(matchingAnnotations);
    }

    protected override string FormatMessage(NormalizedResource resource)
    {
        var count = resource.Annotations.Keys.Count(key =>
            _annotationPrefixes.Any(prefix =>
                key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        return $"Ingress '{resource}' has {count} NGINX-specific annotation(s)";
    }
}
