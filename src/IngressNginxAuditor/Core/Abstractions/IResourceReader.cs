using IngressNginxAuditor.Core.Models;

namespace IngressNginxAuditor.Core.Abstractions;

/// <summary>
/// Interface for reading Kubernetes resources from various sources.
/// Implementations must support streaming via IAsyncEnumerable.
/// </summary>
public interface IResourceReader
{
    /// <summary>
    /// Reads resources asynchronously from the source.
    /// </summary>
    /// <param name="options">Options controlling which resources to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of normalized resources.</returns>
    IAsyncEnumerable<NormalizedResource> ReadResourcesAsync(
        ResourceReaderOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for resource reading operations.
/// </summary>
public sealed record ResourceReaderOptions
{
    /// <summary>Namespaces to include (null = all namespaces).</summary>
    public IReadOnlySet<string>? IncludeNamespaces { get; init; }

    /// <summary>Namespaces to exclude.</summary>
    public IReadOnlySet<string>? ExcludeNamespaces { get; init; }

    /// <summary>Kubernetes label selector expression.</summary>
    public string? LabelSelector { get; init; }

    /// <summary>Resource kinds to scan (null = all supported kinds).</summary>
    public IReadOnlySet<string>? ResourceKinds { get; init; }

    /// <summary>Path to scan (for file-based readers).</summary>
    public string? Path { get; init; }

    /// <summary>Glob patterns for file matching (for file-based readers).</summary>
    public IReadOnlyList<string>? GlobPatterns { get; init; }

    /// <summary>Glob patterns to exclude (for file-based readers).</summary>
    public IReadOnlyList<string>? ExcludePatterns { get; init; }

    /// <summary>Maximum concurrent API calls (for cluster readers).</summary>
    public int MaxConcurrency { get; init; } = 10;

    /// <summary>Timeout for the entire operation.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default options for scanning.
    /// </summary>
    public static ResourceReaderOptions Default => new();
}
