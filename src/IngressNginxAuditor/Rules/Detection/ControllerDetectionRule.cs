using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Rules;

namespace IngressNginxAuditor.Rules.Detection;

/// <summary>
/// DET-NGINX-CTRL-001: Detects ingress-nginx controller components
/// (Deployments, DaemonSets) via standard labels.
/// </summary>
public class ControllerDetectionRule : BaseDetectionRule
{
    // Standard ingress-nginx labels
    private const string AppNameLabel = "app.kubernetes.io/name";
    private const string AppInstanceLabel = "app.kubernetes.io/instance";
    private const string AppComponentLabel = "app.kubernetes.io/component";
    private const string HelmChartLabel = "helm.sh/chart";

    private static readonly string[] ControllerAppNames = new[]
    {
        "ingress-nginx",
        "nginx-ingress",
        "nginx-ingress-controller"
    };

    public override string RuleId => "DET-NGINX-CTRL-001";
    public override string Title => "NGINX Ingress controller component detected";
    public override string Category => "Detection";
    public override Severity DefaultSeverity => Severity.Info;
    public override Confidence DefaultConfidence => Confidence.High;

    public override string Description =>
        "Detects ingress-nginx controller components (Deployments, DaemonSets, Services) " +
        "via standard Kubernetes labels like app.kubernetes.io/name=ingress-nginx or " +
        "Helm chart labels. This indicates an ingress-nginx installation in the cluster.";

    public override IReadOnlyList<string> Recommendations => new[]
    {
        "Document the ingress-nginx controller version and configuration",
        "Identify all Ingress resources depending on this controller",
        "Plan controller replacement with migration timeline",
        "Consider running new and old controllers in parallel during migration"
    };

    protected override bool Matches(NormalizedResource resource)
    {
        // Only check Deployments, DaemonSets, and Services
        if (!IsControllerResourceKind(resource.Kind))
            return false;

        // Check app.kubernetes.io/name label
        if (resource.Labels.TryGetValue(AppNameLabel, out var appName))
        {
            if (ControllerAppNames.Any(cn =>
                cn.Equals(appName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Check helm.sh/chart label for ingress-nginx chart
        if (resource.Labels.TryGetValue(HelmChartLabel, out var chartName))
        {
            if (chartName.StartsWith("ingress-nginx", StringComparison.OrdinalIgnoreCase) ||
                chartName.StartsWith("nginx-ingress", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check for ingress-nginx in app.kubernetes.io/instance
        if (resource.Labels.TryGetValue(AppInstanceLabel, out var instance))
        {
            if (instance.Contains("ingress-nginx", StringComparison.OrdinalIgnoreCase) ||
                instance.Contains("nginx-ingress", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check app.kubernetes.io/component for controller
        if (resource.Labels.TryGetValue(AppComponentLabel, out var component))
        {
            if (component.Equals("controller", StringComparison.OrdinalIgnoreCase) &&
                resource.Labels.TryGetValue(AppNameLabel, out var name) &&
                name.Contains("nginx", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    protected override Evidence CollectEvidence(NormalizedResource resource, bool showAnnotationValues)
    {
        var matchingLabels = new Dictionary<string, string>();
        var matchedPatterns = new List<string>();

        if (resource.Labels.TryGetValue(AppNameLabel, out var appName) &&
            ControllerAppNames.Any(cn => cn.Equals(appName, StringComparison.OrdinalIgnoreCase)))
        {
            matchingLabels[AppNameLabel] = appName;
            matchedPatterns.Add($"{AppNameLabel}={appName}");
        }

        if (resource.Labels.TryGetValue(HelmChartLabel, out var chart) &&
            (chart.StartsWith("ingress-nginx", StringComparison.OrdinalIgnoreCase) ||
             chart.StartsWith("nginx-ingress", StringComparison.OrdinalIgnoreCase)))
        {
            matchingLabels[HelmChartLabel] = chart;
            matchedPatterns.Add($"{HelmChartLabel}={chart}");
        }

        if (resource.Labels.TryGetValue(AppInstanceLabel, out var instance) &&
            (instance.Contains("ingress-nginx", StringComparison.OrdinalIgnoreCase) ||
             instance.Contains("nginx-ingress", StringComparison.OrdinalIgnoreCase)))
        {
            matchingLabels[AppInstanceLabel] = instance;
            matchedPatterns.Add($"{AppInstanceLabel}={instance}");
        }

        return new Evidence
        {
            Labels = matchingLabels,
            MatchedPatterns = matchedPatterns
        };
    }

    protected override string FormatMessage(NormalizedResource resource) =>
        $"NGINX Ingress controller {resource.Kind} '{resource}' detected";

    private static bool IsControllerResourceKind(string kind) =>
        kind.Equals("Deployment", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("DaemonSet", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Service", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("ConfigMap", StringComparison.OrdinalIgnoreCase);
}
