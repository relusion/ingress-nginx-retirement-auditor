namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Normalized representation of a Kubernetes resource.
/// Provides a common structure for resources from cluster or file sources.
/// </summary>
public sealed record NormalizedResource
{
    /// <summary>The Kubernetes resource kind (e.g., "Ingress", "Deployment").</summary>
    public required string Kind { get; init; }

    /// <summary>The Kubernetes API version (e.g., "networking.k8s.io/v1").</summary>
    public required string ApiVersion { get; init; }

    /// <summary>The resource name.</summary>
    public required string Name { get; init; }

    /// <summary>The resource namespace (empty string for cluster-scoped resources).</summary>
    public required string Namespace { get; init; }

    /// <summary>Resource labels (key-value pairs).</summary>
    public required IReadOnlyDictionary<string, string> Labels { get; init; }

    /// <summary>Resource annotations (key-value pairs).</summary>
    public required IReadOnlyDictionary<string, string> Annotations { get; init; }

    /// <summary>The ingressClassName for Ingress resources (null for other resource types).</summary>
    public string? IngressClassName { get; init; }

    /// <summary>Source file path for repo mode (null for cluster mode).</summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Creates a lightweight resource reference from this normalized resource.
    /// </summary>
    public ResourceReference ToResourceReference() => new()
    {
        Kind = Kind,
        ApiVersion = ApiVersion,
        Name = Name,
        Namespace = Namespace
    };

    public override string ToString() =>
        string.IsNullOrEmpty(Namespace)
            ? $"{Kind}/{Name}"
            : $"{Namespace}/{Kind}/{Name}";
}
