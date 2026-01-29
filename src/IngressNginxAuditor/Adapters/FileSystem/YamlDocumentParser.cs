using IngressNginxAuditor.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IngressNginxAuditor.Adapters.FileSystem;

/// <summary>
/// Parses YAML documents and extracts Kubernetes resources.
/// Handles multi-document YAML files and gracefully skips non-K8s content.
/// </summary>
public class YamlDocumentParser
{
    private readonly IDeserializer _deserializer;
    private readonly ILogger<YamlDocumentParser> _logger;

    public YamlDocumentParser(ILogger<YamlDocumentParser>? logger = null)
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        _logger = logger ?? NullLogger<YamlDocumentParser>.Instance;
    }

    /// <summary>
    /// Parses YAML content and yields Kubernetes resources.
    /// </summary>
    /// <param name="content">YAML content (may be multi-document).</param>
    /// <param name="sourcePath">Source file path for error reporting.</param>
    /// <returns>Enumerable of parse results.</returns>
    public IEnumerable<ParseResult> ParseDocuments(string content, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return ParseDocumentsCore(content, sourcePath);
    }

    private List<ParseResult> ParseDocumentsCore(string content, string sourcePath)
    {
        var results = new List<ParseResult>();
        var reader = new StringReader(content);
        var parser = new Parser(reader);

        int documentIndex = 0;

        try
        {
            parser.Consume<StreamStart>();

            while (parser.TryConsume<DocumentStart>(out _))
            {
                documentIndex++;
                ParseResult result;

                try
                {
                    // Check for empty document
                    if (parser.Accept<DocumentEnd>(out _))
                    {
                        parser.Consume<DocumentEnd>();
                        continue;
                    }

                    var document = _deserializer.Deserialize<Dictionary<object, object>>(parser);

                    if (document == null)
                    {
                        // Empty or null document, skip
                        parser.TryConsume<DocumentEnd>(out _);
                        continue;
                    }

                    // Check if it's a Kubernetes resource
                    if (!IsKubernetesResource(document))
                    {
                        _logger.LogDebug(
                            "Skipping non-Kubernetes YAML document {Index} in {Path}",
                            documentIndex, sourcePath);
                        result = ParseResult.Skipped(sourcePath, "Not a Kubernetes resource");
                    }
                    else
                    {
                        var normalized = NormalizeFromYaml(document, sourcePath);
                        result = ParseResult.Success(sourcePath, normalized);
                    }
                }
                catch (YamlException ex)
                {
                    _logger.LogWarning(
                        "YAML parse error in document {Index} of {Path}: {Error}",
                        documentIndex, sourcePath, ex.Message);
                    result = ParseResult.CreateError(sourcePath, $"Document {documentIndex}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Unexpected error parsing document {Index} of {Path}: {Error}",
                        documentIndex, sourcePath, ex.Message);
                    result = ParseResult.CreateError(sourcePath, $"Document {documentIndex}: {ex.Message}");
                }

                results.Add(result);

                // Consume document end if present
                parser.TryConsume<DocumentEnd>(out _);
            }
        }
        catch (YamlException ex)
        {
            _logger.LogWarning("Failed to parse YAML stream in {Path}: {Error}", sourcePath, ex.Message);
            results.Add(ParseResult.CreateError(sourcePath, ex.Message));
        }

        return results;
    }

    private static bool IsKubernetesResource(Dictionary<object, object> doc)
    {
        return doc.ContainsKey("apiVersion") && doc.ContainsKey("kind");
    }

    private static NormalizedResource NormalizeFromYaml(Dictionary<object, object> doc, string sourcePath)
    {
        var apiVersion = GetStringValue(doc, "apiVersion") ?? string.Empty;
        var kind = GetStringValue(doc, "kind") ?? string.Empty;

        var metadata = GetDictionary(doc, "metadata");
        var name = GetStringValue(metadata, "name") ?? string.Empty;
        var ns = GetStringValue(metadata, "namespace") ?? "default";
        var labels = GetStringDictionary(metadata, "labels");
        var annotations = GetStringDictionary(metadata, "annotations");

        // For Ingress, extract ingressClassName
        string? ingressClassName = null;
        if (kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
        {
            var spec = GetDictionary(doc, "spec");
            ingressClassName = GetStringValue(spec, "ingressClassName");
        }

        return new NormalizedResource
        {
            Kind = kind,
            ApiVersion = apiVersion,
            Name = name,
            Namespace = ns,
            Labels = labels,
            Annotations = annotations,
            IngressClassName = ingressClassName,
            SourcePath = sourcePath
        };
    }

    private static string? GetStringValue(Dictionary<object, object>? dict, string key)
    {
        if (dict == null) return null;
        if (dict.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }
        return null;
    }

    private static Dictionary<object, object>? GetDictionary(Dictionary<object, object>? dict, string key)
    {
        if (dict == null) return null;
        if (dict.TryGetValue(key, out var value) && value is Dictionary<object, object> nested)
        {
            return nested;
        }
        return null;
    }

    private static IReadOnlyDictionary<string, string> GetStringDictionary(
        Dictionary<object, object>? dict, string key)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (dict == null) return result;
        if (!dict.TryGetValue(key, out var value)) return result;

        if (value is Dictionary<object, object> nested)
        {
            foreach (var kvp in nested)
            {
                var k = kvp.Key?.ToString();
                var v = kvp.Value?.ToString();
                if (!string.IsNullOrEmpty(k) && v != null)
                {
                    result[k] = v;
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Result of parsing a YAML document.
/// </summary>
public sealed record ParseResult
{
    public required string SourcePath { get; init; }
    public required ParseResultStatus Status { get; init; }
    public NormalizedResource? Resource { get; init; }
    public string? Error { get; init; }
    public string? SkipReason { get; init; }

    public static ParseResult Success(string path, NormalizedResource resource) => new()
    {
        SourcePath = path,
        Status = ParseResultStatus.Success,
        Resource = resource
    };

    public static ParseResult Skipped(string path, string reason) => new()
    {
        SourcePath = path,
        Status = ParseResultStatus.Skipped,
        SkipReason = reason
    };

    public static ParseResult CreateError(string path, string error) => new()
    {
        SourcePath = path,
        Status = ParseResultStatus.Error,
        Error = error
    };
}

/// <summary>
/// Status of a parse result.
/// </summary>
public enum ParseResultStatus
{
    Success,
    Skipped,
    Error
}
