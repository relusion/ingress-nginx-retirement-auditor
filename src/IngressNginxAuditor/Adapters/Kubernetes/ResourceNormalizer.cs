using IngressNginxAuditor.Core.Models;
using k8s.Models;

namespace IngressNginxAuditor.Adapters.Kubernetes;

/// <summary>
/// Normalizes Kubernetes resources to the internal NormalizedResource model.
/// </summary>
public static class ResourceNormalizer
{
    /// <summary>
    /// Normalizes a V1Ingress to NormalizedResource.
    /// </summary>
    public static NormalizedResource FromIngress(V1Ingress ingress)
    {
        return new NormalizedResource
        {
            Kind = "Ingress",
            ApiVersion = ingress.ApiVersion ?? "networking.k8s.io/v1",
            Name = ingress.Metadata?.Name ?? string.Empty,
            Namespace = ingress.Metadata?.NamespaceProperty ?? "default",
            Labels = ToDictionary(ingress.Metadata?.Labels),
            Annotations = ToDictionary(ingress.Metadata?.Annotations),
            IngressClassName = ingress.Spec?.IngressClassName,
            SourcePath = null // Cluster mode, no source file
        };
    }

    /// <summary>
    /// Normalizes a V1Deployment to NormalizedResource.
    /// </summary>
    public static NormalizedResource FromDeployment(V1Deployment deployment)
    {
        return new NormalizedResource
        {
            Kind = "Deployment",
            ApiVersion = deployment.ApiVersion ?? "apps/v1",
            Name = deployment.Metadata?.Name ?? string.Empty,
            Namespace = deployment.Metadata?.NamespaceProperty ?? "default",
            Labels = ToDictionary(deployment.Metadata?.Labels),
            Annotations = ToDictionary(deployment.Metadata?.Annotations),
            IngressClassName = null,
            SourcePath = null
        };
    }

    /// <summary>
    /// Normalizes a V1DaemonSet to NormalizedResource.
    /// </summary>
    public static NormalizedResource FromDaemonSet(V1DaemonSet daemonSet)
    {
        return new NormalizedResource
        {
            Kind = "DaemonSet",
            ApiVersion = daemonSet.ApiVersion ?? "apps/v1",
            Name = daemonSet.Metadata?.Name ?? string.Empty,
            Namespace = daemonSet.Metadata?.NamespaceProperty ?? "default",
            Labels = ToDictionary(daemonSet.Metadata?.Labels),
            Annotations = ToDictionary(daemonSet.Metadata?.Annotations),
            IngressClassName = null,
            SourcePath = null
        };
    }

    /// <summary>
    /// Normalizes a V1Service to NormalizedResource.
    /// </summary>
    public static NormalizedResource FromService(V1Service service)
    {
        return new NormalizedResource
        {
            Kind = "Service",
            ApiVersion = service.ApiVersion ?? "v1",
            Name = service.Metadata?.Name ?? string.Empty,
            Namespace = service.Metadata?.NamespaceProperty ?? "default",
            Labels = ToDictionary(service.Metadata?.Labels),
            Annotations = ToDictionary(service.Metadata?.Annotations),
            IngressClassName = null,
            SourcePath = null
        };
    }

    /// <summary>
    /// Normalizes a V1ConfigMap to NormalizedResource.
    /// </summary>
    public static NormalizedResource FromConfigMap(V1ConfigMap configMap)
    {
        return new NormalizedResource
        {
            Kind = "ConfigMap",
            ApiVersion = configMap.ApiVersion ?? "v1",
            Name = configMap.Metadata?.Name ?? string.Empty,
            Namespace = configMap.Metadata?.NamespaceProperty ?? "default",
            Labels = ToDictionary(configMap.Metadata?.Labels),
            Annotations = ToDictionary(configMap.Metadata?.Annotations),
            IngressClassName = null,
            SourcePath = null
        };
    }

    private static IReadOnlyDictionary<string, string> ToDictionary(IDictionary<string, string>? source)
    {
        if (source == null || source.Count == 0)
            return new Dictionary<string, string>();

        return new Dictionary<string, string>(source, StringComparer.Ordinal);
    }
}
