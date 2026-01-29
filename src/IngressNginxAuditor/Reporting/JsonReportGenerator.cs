using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IngressNginxAuditor.Configuration;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Reporting;

/// <summary>
/// Generates JSON reports from scan results.
/// </summary>
public class JsonReportGenerator : IReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Format => "json";
    public string FileExtension => ".json";

    public async Task GenerateAsync(
        ScanResult result,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var report = CreateJsonReport(result);
        await JsonSerializer.SerializeAsync(outputStream, report, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<string> GenerateToStringAsync(
        ScanResult result,
        CancellationToken cancellationToken = default)
    {
        var report = CreateJsonReport(result);
        return Task.FromResult(JsonSerializer.Serialize(report, JsonOptions));
    }

    private static JsonReport CreateJsonReport(ScanResult result)
    {
        return new JsonReport
        {
            Schema = $"https://github.com/example/ingress-nginx-auditor/schemas/report-{DefaultConfiguration.SchemaVersion}.json",
            Metadata = CreateMetadata(result.Metadata),
            Summary = CreateSummary(result.Summary),
            Findings = result.Findings.Select(CreateFinding).OrderBy(f => f.Severity).ThenBy(f => f.Resource.Namespace).ToList(),
            Warnings = result.Warnings.ToList(),
            Errors = result.Errors.ToList()
        };
    }

    private static JsonMetadata CreateMetadata(ScanMetadata metadata)
    {
        return new JsonMetadata
        {
            Tool = metadata.Tool,
            Version = metadata.Version,
            SchemaVersion = metadata.SchemaVersion,
            Mode = metadata.Mode.ToString().ToLowerInvariant(),
            Status = metadata.Status,
            TimestampUtc = metadata.TimestampUtc,
            DurationMs = metadata.Duration?.TotalMilliseconds,
            ClusterContext = metadata.ClusterContext,
            ScannedPath = metadata.ScannedPath
        };
    }

    private static JsonSummary CreateSummary(ScanSummary summary)
    {
        return new JsonSummary
        {
            ResourcesScanned = summary.ResourcesScanned,
            IngressesScanned = summary.IngressesScanned,
            NginxDependentIngresses = summary.NginxDependentIngresses,
            TotalFindings = summary.TotalFindings,
            FindingsBySeverity = summary.FindingsBySeverity.ToDictionary(
                kvp => kvp.Key.ToString().ToLowerInvariant(),
                kvp => kvp.Value),
            ByNamespace = summary.ByNamespace.ToDictionary(
                kvp => kvp.Key,
                kvp => CreateNamespaceRollup(kvp.Value)),
            Policy = CreatePolicyResult(summary.Policy)
        };
    }

    private static JsonNamespaceRollup CreateNamespaceRollup(NamespaceRollup rollup)
    {
        return new JsonNamespaceRollup
        {
            IngressCount = rollup.IngressCount,
            FindingCount = rollup.FindingCount,
            MaxRiskScore = rollup.MaxRiskScore,
            BySeverity = rollup.BySeverity.ToDictionary(
                kvp => kvp.Key.ToString().ToLowerInvariant(),
                kvp => kvp.Value)
        };
    }

    private static JsonPolicyResult CreatePolicyResult(PolicyResult policy)
    {
        return new JsonPolicyResult
        {
            Passed = policy.Passed,
            FailOnSeverity = policy.FailOnSeverity.ToString().ToLowerInvariant(),
            ViolationCount = policy.ViolationCount,
            ExitCode = policy.ExitCode
        };
    }

    private static JsonFinding CreateFinding(Finding finding)
    {
        return new JsonFinding
        {
            Id = finding.Id,
            Title = finding.Title,
            Severity = finding.Severity.ToString().ToLowerInvariant(),
            Confidence = finding.Confidence.ToString().ToLowerInvariant(),
            Category = finding.Category,
            Resource = CreateResourceReference(finding.Resource),
            Evidence = CreateEvidence(finding.Evidence),
            Message = finding.Message,
            Recommendations = finding.Recommendations.ToList()
        };
    }

    private static JsonResourceReference CreateResourceReference(ResourceReference resource)
    {
        return new JsonResourceReference
        {
            Kind = resource.Kind,
            ApiVersion = resource.ApiVersion,
            Name = resource.Name,
            Namespace = resource.Namespace
        };
    }

    private static JsonEvidence CreateEvidence(Evidence evidence)
    {
        return new JsonEvidence
        {
            Annotations = evidence.Annotations?.ToDictionary(
                kvp => kvp.Key,
                kvp => CreateRedactedAnnotation(kvp.Value)),
            Labels = evidence.Labels,
            IngressClassName = evidence.IngressClassName,
            MatchedPatterns = evidence.MatchedPatterns?.ToList()
        };
    }

    private static JsonRedactedAnnotation CreateRedactedAnnotation(RedactedAnnotation annotation)
    {
        return new JsonRedactedAnnotation
        {
            Length = annotation.ValueLength,
            HashPrefix = annotation.ValueHashPrefix,
            Preview = annotation.TruncatedValue
        };
    }

    // JSON model classes for serialization
    private sealed class JsonReport
    {
        [JsonPropertyName("$schema")]
        public required string Schema { get; init; }
        public required JsonMetadata Metadata { get; init; }
        public required JsonSummary Summary { get; init; }
        public required List<JsonFinding> Findings { get; init; }
        public List<string>? Warnings { get; init; }
        public List<string>? Errors { get; init; }
    }

    private sealed class JsonMetadata
    {
        public required string Tool { get; init; }
        public required string Version { get; init; }
        public required string SchemaVersion { get; init; }
        public required string Mode { get; init; }
        public required string Status { get; init; }
        public required DateTimeOffset TimestampUtc { get; init; }
        public double? DurationMs { get; init; }
        public string? ClusterContext { get; init; }
        public string? ScannedPath { get; init; }
    }

    private sealed class JsonSummary
    {
        public required int ResourcesScanned { get; init; }
        public required int IngressesScanned { get; init; }
        public required int NginxDependentIngresses { get; init; }
        public required int TotalFindings { get; init; }
        public required Dictionary<string, int> FindingsBySeverity { get; init; }
        public required Dictionary<string, JsonNamespaceRollup> ByNamespace { get; init; }
        public required JsonPolicyResult Policy { get; init; }
    }

    private sealed class JsonNamespaceRollup
    {
        public required int IngressCount { get; init; }
        public required int FindingCount { get; init; }
        public required int MaxRiskScore { get; init; }
        public required Dictionary<string, int> BySeverity { get; init; }
    }

    private sealed class JsonPolicyResult
    {
        public required bool Passed { get; init; }
        public required string FailOnSeverity { get; init; }
        public required int ViolationCount { get; init; }
        public required int ExitCode { get; init; }
    }

    private sealed class JsonFinding
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string Severity { get; init; }
        public required string Confidence { get; init; }
        public required string Category { get; init; }
        public required JsonResourceReference Resource { get; init; }
        public required JsonEvidence Evidence { get; init; }
        public required string Message { get; init; }
        public required List<string> Recommendations { get; init; }
    }

    private sealed class JsonResourceReference
    {
        public required string Kind { get; init; }
        public required string ApiVersion { get; init; }
        public required string Name { get; init; }
        public required string Namespace { get; init; }
    }

    private sealed class JsonEvidence
    {
        public Dictionary<string, JsonRedactedAnnotation>? Annotations { get; init; }
        public IReadOnlyDictionary<string, string>? Labels { get; init; }
        public string? IngressClassName { get; init; }
        public List<string>? MatchedPatterns { get; init; }
    }

    private sealed class JsonRedactedAnnotation
    {
        public required int Length { get; init; }
        public required string HashPrefix { get; init; }
        public string? Preview { get; init; }
    }
}
