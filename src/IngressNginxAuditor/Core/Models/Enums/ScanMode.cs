namespace IngressNginxAuditor.Core.Models.Enums;

/// <summary>
/// The mode of scanning operation.
/// </summary>
public enum ScanMode
{
    /// <summary>Scan resources from a live Kubernetes cluster.</summary>
    Cluster,

    /// <summary>Scan resources from YAML files in a repository or stdin.</summary>
    Repo
}
