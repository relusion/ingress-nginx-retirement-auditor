using System.Runtime.CompilerServices;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngressNginxAuditor.Adapters.FileSystem;

/// <summary>
/// Reads Kubernetes resources from YAML files in a directory.
/// Implements IResourceReader with streaming via IAsyncEnumerable.
/// </summary>
public class FileResourceReader : IResourceReader
{
    private readonly GlobMatcher _globMatcher;
    private readonly YamlDocumentParser _parser;
    private readonly ILogger<FileResourceReader> _logger;
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    public FileResourceReader(
        GlobMatcher? globMatcher = null,
        YamlDocumentParser? parser = null,
        ILogger<FileResourceReader>? logger = null)
    {
        _globMatcher = globMatcher ?? new GlobMatcher();
        _parser = parser ?? new YamlDocumentParser();
        _logger = logger ?? NullLogger<FileResourceReader>.Instance;
    }

    /// <summary>
    /// Gets warnings collected during scanning.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Gets errors collected during scanning.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <inheritdoc />
    public async IAsyncEnumerable<NormalizedResource> ReadResourcesAsync(
        ResourceReaderOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _warnings.Clear();
        _errors.Clear();

        var basePath = options.Path ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(basePath))
        {
            _errors.Add($"Directory not found: {basePath}");
            yield break;
        }

        // Find all matching files
        IReadOnlyList<string> files;
        try
        {
            files = _globMatcher.MatchFiles(
                basePath,
                options.GlobPatterns,
                options.ExcludePatterns);
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to enumerate files: {ex.Message}");
            yield break;
        }

        if (files.Count == 0)
        {
            _warnings.Add($"No YAML files found in {basePath}");
            yield break;
        }

        _logger.LogInformation("Found {FileCount} YAML files to scan", files.Count);

        // Process files in parallel with bounded concurrency
        var channel = System.Threading.Channels.Channel.CreateBounded<NormalizedResource>(
            new System.Threading.Channels.BoundedChannelOptions(100)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
            });

        var processingTask = ProcessFilesAsync(files, channel.Writer, cancellationToken);

        await foreach (var resource in channel.Reader.ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return resource;
        }

        await processingTask.ConfigureAwait(false);
    }

    private async Task ProcessFilesAsync(
        IReadOnlyList<string> files,
        System.Threading.Channels.ChannelWriter<NormalizedResource> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await Parallel.ForEachAsync(
                files,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken
                },
                async (file, ct) =>
                {
                    await ProcessFileAsync(file, writer, ct).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task ProcessFileAsync(
        string filePath,
        System.Threading.Channels.ChannelWriter<NormalizedResource> writer,
        CancellationToken cancellationToken)
    {
        string content;
        try
        {
            content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (_errors)
            {
                _errors.Add($"Failed to read file {filePath}: {ex.Message}");
            }
            return;
        }

        foreach (var result in _parser.ParseDocuments(content, filePath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (result.Status)
            {
                case ParseResultStatus.Success when result.Resource != null:
                    await writer.WriteAsync(result.Resource, cancellationToken).ConfigureAwait(false);
                    break;

                case ParseResultStatus.Skipped:
                    _logger.LogDebug("Skipped {Path}: {Reason}", result.SourcePath, result.SkipReason);
                    break;

                case ParseResultStatus.Error:
                    lock (_errors)
                    {
                        _errors.Add($"Parse error in {result.SourcePath}: {result.Error}");
                    }
                    break;
            }
        }
    }
}
