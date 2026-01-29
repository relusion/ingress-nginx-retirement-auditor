using System.Runtime.CompilerServices;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngressNginxAuditor.Adapters.Kubernetes;

/// <summary>
/// Kubernetes client wrapper with bounded concurrency for API calls.
/// Implements ADR-005 for rate limit safety.
/// </summary>
public class BoundedKubernetesClient : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly IKubernetes _client;
    private readonly ILogger<BoundedKubernetesClient> _logger;
    private bool _disposed;

    public BoundedKubernetesClient(
        IKubernetes client,
        int maxConcurrency = 10,
        ILogger<BoundedKubernetesClient>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _logger = logger ?? NullLogger<BoundedKubernetesClient>.Instance;
    }

    /// <summary>
    /// Lists namespaces in the cluster.
    /// </summary>
    public async Task<V1NamespaceList> ListNamespacesAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await _client.CoreV1.ListNamespaceAsync(cancellationToken: ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Lists Ingresses across specified namespaces with bounded concurrency.
    /// </summary>
    public async IAsyncEnumerable<NamespaceResult<V1Ingress>> ListIngressesAsync(
        IEnumerable<string> namespaces,
        string? labelSelector = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tasks = namespaces.Select(ns => FetchIngressesAsync(ns, labelSelector, ct));

        await foreach (var task in WhenEachAsync(tasks, ct).ConfigureAwait(false))
        {
            yield return await task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Lists Deployments across specified namespaces with bounded concurrency.
    /// </summary>
    public async IAsyncEnumerable<NamespaceResult<V1Deployment>> ListDeploymentsAsync(
        IEnumerable<string> namespaces,
        string? labelSelector = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tasks = namespaces.Select(ns => FetchDeploymentsAsync(ns, labelSelector, ct));

        await foreach (var task in WhenEachAsync(tasks, ct).ConfigureAwait(false))
        {
            yield return await task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Lists DaemonSets across specified namespaces with bounded concurrency.
    /// </summary>
    public async IAsyncEnumerable<NamespaceResult<V1DaemonSet>> ListDaemonSetsAsync(
        IEnumerable<string> namespaces,
        string? labelSelector = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tasks = namespaces.Select(ns => FetchDaemonSetsAsync(ns, labelSelector, ct));

        await foreach (var task in WhenEachAsync(tasks, ct).ConfigureAwait(false))
        {
            yield return await task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Lists Services across specified namespaces with bounded concurrency.
    /// </summary>
    public async IAsyncEnumerable<NamespaceResult<V1Service>> ListServicesAsync(
        IEnumerable<string> namespaces,
        string? labelSelector = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tasks = namespaces.Select(ns => FetchServicesAsync(ns, labelSelector, ct));

        await foreach (var task in WhenEachAsync(tasks, ct).ConfigureAwait(false))
        {
            yield return await task.ConfigureAwait(false);
        }
    }

    private async Task<NamespaceResult<V1Ingress>> FetchIngressesAsync(
        string ns, string? labelSelector, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _logger.LogDebug("Fetching Ingresses from namespace {Namespace}", ns);
            var list = await _client.NetworkingV1.ListNamespacedIngressAsync(
                ns, labelSelector: labelSelector, cancellationToken: ct).ConfigureAwait(false);
            return NamespaceResult<V1Ingress>.Success(ns, list.Items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to list Ingresses in namespace {Namespace}", ns);
            return NamespaceResult<V1Ingress>.Failure(ns, ex.Message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<NamespaceResult<V1Deployment>> FetchDeploymentsAsync(
        string ns, string? labelSelector, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = await _client.AppsV1.ListNamespacedDeploymentAsync(
                ns, labelSelector: labelSelector, cancellationToken: ct).ConfigureAwait(false);
            return NamespaceResult<V1Deployment>.Success(ns, list.Items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to list Deployments in namespace {Namespace}", ns);
            return NamespaceResult<V1Deployment>.Failure(ns, ex.Message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<NamespaceResult<V1DaemonSet>> FetchDaemonSetsAsync(
        string ns, string? labelSelector, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = await _client.AppsV1.ListNamespacedDaemonSetAsync(
                ns, labelSelector: labelSelector, cancellationToken: ct).ConfigureAwait(false);
            return NamespaceResult<V1DaemonSet>.Success(ns, list.Items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to list DaemonSets in namespace {Namespace}", ns);
            return NamespaceResult<V1DaemonSet>.Failure(ns, ex.Message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<NamespaceResult<V1Service>> FetchServicesAsync(
        string ns, string? labelSelector, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var list = await _client.CoreV1.ListNamespacedServiceAsync(
                ns, labelSelector: labelSelector, cancellationToken: ct).ConfigureAwait(false);
            return NamespaceResult<V1Service>.Success(ns, list.Items);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to list Services in namespace {Namespace}", ns);
            return NamespaceResult<V1Service>.Failure(ns, ex.Message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static async IAsyncEnumerable<Task<T>> WhenEachAsync<T>(
        IEnumerable<Task<T>> tasks,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var taskList = tasks.ToList();
        var remaining = new HashSet<Task<T>>(taskList);

        while (remaining.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var completed = await Task.WhenAny(remaining).ConfigureAwait(false);
            remaining.Remove(completed);
            yield return completed;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Result of a namespace-scoped resource fetch operation.
/// </summary>
public sealed record NamespaceResult<T>
{
    public required string Namespace { get; init; }
    public required IReadOnlyList<T> Items { get; init; }
    public required bool IsSuccess { get; init; }
    public string? Error { get; init; }

    public static NamespaceResult<T> Success(string ns, IList<T> items) => new()
    {
        Namespace = ns,
        Items = items.ToList(),
        IsSuccess = true
    };

    public static NamespaceResult<T> Failure(string ns, string error) => new()
    {
        Namespace = ns,
        Items = Array.Empty<T>(),
        IsSuccess = false,
        Error = error
    };
}
