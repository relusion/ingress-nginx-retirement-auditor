using System.Runtime.CompilerServices;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngressNginxAuditor.Adapters.Kubernetes;

/// <summary>
/// Reads Kubernetes resources from a cluster via the API server.
/// Implements IResourceReader with streaming via IAsyncEnumerable.
/// </summary>
public class ClusterResourceReader : IResourceReader, IDisposable
{
    private readonly BoundedKubernetesClient _client;
    private readonly ILogger<ClusterResourceReader> _logger;
    private readonly List<string> _warnings = [];
    private bool _disposed;

    public ClusterResourceReader(
        BoundedKubernetesClient client,
        ILogger<ClusterResourceReader>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? NullLogger<ClusterResourceReader>.Instance;
    }

    /// <summary>
    /// Gets warnings collected during scanning.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <inheritdoc />
    public async IAsyncEnumerable<NormalizedResource> ReadResourcesAsync(
        ResourceReaderOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _warnings.Clear();

        // Get namespaces to scan
        var namespaces = await GetNamespacesToScanAsync(options, cancellationToken)
            .ConfigureAwait(false);

        if (namespaces.Count == 0)
        {
            _warnings.Add("No namespaces available to scan");
            yield break;
        }

        _logger.LogInformation("Scanning {NamespaceCount} namespaces", namespaces.Count);

        // Scan Ingresses
        await foreach (var resource in ScanIngressesAsync(namespaces, options, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return resource;
        }

        // Scan controller components (Deployments, DaemonSets, Services)
        var resourceKinds = options.ResourceKinds;
        if (resourceKinds == null || resourceKinds.Contains("Deployment"))
        {
            await foreach (var resource in ScanDeploymentsAsync(namespaces, options, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return resource;
            }
        }

        if (resourceKinds == null || resourceKinds.Contains("DaemonSet"))
        {
            await foreach (var resource in ScanDaemonSetsAsync(namespaces, options, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return resource;
            }
        }

        if (resourceKinds == null || resourceKinds.Contains("Service"))
        {
            await foreach (var resource in ScanServicesAsync(namespaces, options, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return resource;
            }
        }
    }

    private async Task<IReadOnlyList<string>> GetNamespacesToScanAsync(
        ResourceReaderOptions options,
        CancellationToken cancellationToken)
    {
        // If specific namespaces are requested, use them directly
        if (options.IncludeNamespaces?.Count > 0)
        {
            return options.IncludeNamespaces
                .Where(ns => !(options.ExcludeNamespaces?.Contains(ns) ?? false))
                .ToList();
        }

        // Otherwise, list all namespaces
        try
        {
            var nsList = await _client.ListNamespacesAsync(cancellationToken)
                .ConfigureAwait(false);

            return nsList.Items
                .Select(ns => ns.Metadata?.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .Where(name => !(options.ExcludeNamespaces?.Contains(name!) ?? false))
                .Cast<string>()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list namespaces");
            _warnings.Add($"Failed to list namespaces: {ex.Message}");
            return [];
        }
    }

    private async IAsyncEnumerable<NormalizedResource> ScanIngressesAsync(
        IReadOnlyList<string> namespaces,
        ResourceReaderOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var result in _client.ListIngressesAsync(
            namespaces, options.LabelSelector, cancellationToken).ConfigureAwait(false))
        {
            if (!result.IsSuccess)
            {
                _warnings.Add($"Failed to list Ingresses in namespace '{result.Namespace}': {result.Error}");
                continue;
            }

            foreach (var ingress in result.Items)
            {
                yield return ResourceNormalizer.FromIngress(ingress);
            }
        }
    }

    private async IAsyncEnumerable<NormalizedResource> ScanDeploymentsAsync(
        IReadOnlyList<string> namespaces,
        ResourceReaderOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var result in _client.ListDeploymentsAsync(
            namespaces, options.LabelSelector, cancellationToken).ConfigureAwait(false))
        {
            if (!result.IsSuccess)
            {
                _warnings.Add($"Failed to list Deployments in namespace '{result.Namespace}': {result.Error}");
                continue;
            }

            foreach (var deployment in result.Items)
            {
                yield return ResourceNormalizer.FromDeployment(deployment);
            }
        }
    }

    private async IAsyncEnumerable<NormalizedResource> ScanDaemonSetsAsync(
        IReadOnlyList<string> namespaces,
        ResourceReaderOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var result in _client.ListDaemonSetsAsync(
            namespaces, options.LabelSelector, cancellationToken).ConfigureAwait(false))
        {
            if (!result.IsSuccess)
            {
                _warnings.Add($"Failed to list DaemonSets in namespace '{result.Namespace}': {result.Error}");
                continue;
            }

            foreach (var daemonSet in result.Items)
            {
                yield return ResourceNormalizer.FromDaemonSet(daemonSet);
            }
        }
    }

    private async IAsyncEnumerable<NormalizedResource> ScanServicesAsync(
        IReadOnlyList<string> namespaces,
        ResourceReaderOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var result in _client.ListServicesAsync(
            namespaces, options.LabelSelector, cancellationToken).ConfigureAwait(false))
        {
            if (!result.IsSuccess)
            {
                _warnings.Add($"Failed to list Services in namespace '{result.Namespace}': {result.Error}");
                continue;
            }

            foreach (var service in result.Items)
            {
                yield return ResourceNormalizer.FromService(service);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
