namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Lightweight reference to a Kubernetes resource.
/// Used in findings to identify the source resource.
/// </summary>
public sealed record ResourceReference
{
    /// <summary>The Kubernetes resource kind (e.g., "Ingress").</summary>
    public required string Kind { get; init; }

    /// <summary>The Kubernetes API version (e.g., "networking.k8s.io/v1").</summary>
    public required string ApiVersion { get; init; }

    /// <summary>The resource name.</summary>
    public required string Name { get; init; }

    /// <summary>The resource namespace (empty string for cluster-scoped resources).</summary>
    public required string Namespace { get; init; }

    public override string ToString() =>
        string.IsNullOrEmpty(Namespace)
            ? $"{Kind}/{Name}"
            : $"{Namespace}/{Kind}/{Name}";
}
