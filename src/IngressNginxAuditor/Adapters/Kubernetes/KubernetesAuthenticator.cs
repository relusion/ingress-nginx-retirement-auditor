using k8s;

namespace IngressNginxAuditor.Adapters.Kubernetes;

/// <summary>
/// Handles Kubernetes authentication following the fallback chain from EAR-C001.
/// </summary>
public class KubernetesAuthenticator
{
    /// <summary>
    /// Creates a Kubernetes client using the authentication fallback chain.
    /// </summary>
    /// <param name="kubeconfigPath">Explicit kubeconfig path (highest priority).</param>
    /// <param name="context">Specific context to use.</param>
    /// <returns>A configured IKubernetes client.</returns>
    /// <exception cref="InvalidOperationException">When no valid configuration is found.</exception>
    public IKubernetes CreateClient(string? kubeconfigPath = null, string? context = null)
    {
        var config = ResolveConfig(kubeconfigPath, context);
        return new k8s.Kubernetes(config);
    }

    /// <summary>
    /// Resolves Kubernetes configuration using the fallback chain.
    /// </summary>
    public KubernetesClientConfiguration ResolveConfig(string? kubeconfigPath = null, string? context = null)
    {
        // Priority 1: Explicit --kubeconfig flag
        if (!string.IsNullOrEmpty(kubeconfigPath))
        {
            if (!File.Exists(kubeconfigPath))
            {
                throw new FileNotFoundException(
                    $"Kubeconfig file not found at specified path: {kubeconfigPath}",
                    kubeconfigPath);
            }

            return LoadFromFile(kubeconfigPath, context);
        }

        // Priority 2: KUBECONFIG environment variable
        var envKubeconfig = Environment.GetEnvironmentVariable("KUBECONFIG");
        if (!string.IsNullOrEmpty(envKubeconfig) && File.Exists(envKubeconfig))
        {
            return LoadFromFile(envKubeconfig, context);
        }

        // Priority 3: Default kubeconfig location
        var defaultPath = GetDefaultKubeconfigPath();
        if (File.Exists(defaultPath))
        {
            return LoadFromFile(defaultPath, context);
        }

        // Priority 4: In-cluster config
        if (KubernetesClientConfiguration.IsInCluster())
        {
            return KubernetesClientConfiguration.InClusterConfig();
        }

        // No configuration found
        throw new InvalidOperationException(
            "Could not find Kubernetes configuration. " +
            "Provide --kubeconfig, set KUBECONFIG environment variable, " +
            "ensure ~/.kube/config exists, or run in-cluster.");
    }

    /// <summary>
    /// Gets available contexts from the default kubeconfig.
    /// </summary>
    public IEnumerable<string> GetAvailableContexts(string? kubeconfigPath = null)
    {
        var path = kubeconfigPath
            ?? Environment.GetEnvironmentVariable("KUBECONFIG")
            ?? GetDefaultKubeconfigPath();

        if (!File.Exists(path))
            return Enumerable.Empty<string>();

        try
        {
            var config = KubernetesClientConfiguration.LoadKubeConfig(path);
            return config.Contexts?.Select(c => c.Name) ?? Enumerable.Empty<string>();
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Validates that a context exists in the kubeconfig.
    /// </summary>
    public bool ValidateContext(string context, string? kubeconfigPath = null)
    {
        var contexts = GetAvailableContexts(kubeconfigPath);
        return contexts.Contains(context, StringComparer.Ordinal);
    }

    private static KubernetesClientConfiguration LoadFromFile(string path, string? context)
    {
        if (!string.IsNullOrEmpty(context))
        {
            var config = KubernetesClientConfiguration.LoadKubeConfig(path);
            var contextExists = config.Contexts?.Any(c =>
                c.Name.Equals(context, StringComparison.Ordinal)) ?? false;

            if (!contextExists)
            {
                var available = config.Contexts?.Select(c => c.Name) ?? Enumerable.Empty<string>();
                throw new ArgumentException(
                    $"Context '{context}' not found in kubeconfig. " +
                    $"Available contexts: {string.Join(", ", available)}");
            }

            return KubernetesClientConfiguration.BuildConfigFromConfigFile(path, context);
        }

        return KubernetesClientConfiguration.BuildConfigFromConfigFile(path);
    }

    private static string GetDefaultKubeconfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".kube", "config");
    }
}
