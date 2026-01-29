using System.Runtime.CompilerServices;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngressNginxAuditor.Adapters.FileSystem;

/// <summary>
/// Reads Kubernetes resources from standard input (stdin).
/// Supports piped input from Helm, Kustomize, or other tools.
/// </summary>
public class StdinResourceReader : IResourceReader
{
    private readonly YamlDocumentParser _parser;
    private readonly ILogger<StdinResourceReader> _logger;
    private readonly TextReader _input;
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    public StdinResourceReader(
        TextReader? input = null,
        YamlDocumentParser? parser = null,
        ILogger<StdinResourceReader>? logger = null)
    {
        _input = input ?? Console.In;
        _parser = parser ?? new YamlDocumentParser();
        _logger = logger ?? NullLogger<StdinResourceReader>.Instance;
    }

    /// <summary>
    /// Gets warnings collected during scanning.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Gets errors collected during scanning.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Whether stdin had any content.
    /// </summary>
    public bool HadInput { get; private set; }

    /// <inheritdoc />
    public async IAsyncEnumerable<NormalizedResource> ReadResourcesAsync(
        ResourceReaderOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _warnings.Clear();
        _errors.Clear();
        HadInput = false;

        // Read all content from stdin
        string content;
        try
        {
            content = await _input.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to read from stdin: {ex.Message}");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("No input received from stdin");
            // Don't add to errors - this is handled by exit code 3
            yield break;
        }

        HadInput = true;
        _logger.LogInformation("Reading resources from stdin ({Length} bytes)", content.Length);

        // Parse the YAML content
        foreach (var result in _parser.ParseDocuments(content, "<stdin>"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (result.Status)
            {
                case ParseResultStatus.Success when result.Resource != null:
                    yield return result.Resource;
                    break;

                case ParseResultStatus.Skipped:
                    _logger.LogDebug("Skipped stdin document: {Reason}", result.SkipReason);
                    break;

                case ParseResultStatus.Error:
                    _errors.Add($"Parse error in stdin: {result.Error}");
                    break;
            }
        }
    }
}
