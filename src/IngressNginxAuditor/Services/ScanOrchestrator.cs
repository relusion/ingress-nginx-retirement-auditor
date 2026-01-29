using System.Diagnostics;
using IngressNginxAuditor.Configuration;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Reporting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngressNginxAuditor.Services;

/// <summary>
/// Orchestrates the scan workflow: reading resources, applying rules, and generating reports.
/// </summary>
public class ScanOrchestrator
{
    private readonly RuleEngine _ruleEngine;
    private readonly PolicyEvaluator _policyEvaluator;
    private readonly IEnumerable<IReportGenerator> _reportGenerators;
    private readonly ILogger<ScanOrchestrator> _logger;

    public ScanOrchestrator(
        RuleEngine ruleEngine,
        PolicyEvaluator policyEvaluator,
        IEnumerable<IReportGenerator> reportGenerators,
        ILogger<ScanOrchestrator>? logger = null)
    {
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _policyEvaluator = policyEvaluator ?? throw new ArgumentNullException(nameof(policyEvaluator));
        _reportGenerators = reportGenerators ?? throw new ArgumentNullException(nameof(reportGenerators));
        _logger = logger ?? NullLogger<ScanOrchestrator>.Instance;
    }

    /// <summary>
    /// Executes a scan using the provided resource reader.
    /// </summary>
    public async Task<ScanResult> ScanAsync(
        IResourceReader reader,
        ResourceReaderOptions readerOptions,
        AuditorConfig config,
        ScanMode mode,
        string? context = null,
        string? scannedPath = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var aggregator = new FindingAggregator();
        var findings = new List<Finding>();
        var warnings = new List<string>();
        var errors = new List<string>();
        var showAnnotationValues = config.Output.ShowAnnotationValues;

        _logger.LogInformation("Starting {Mode} scan", mode);

        try
        {
            await foreach (var resource in reader.ReadResourcesAsync(readerOptions, cancellationToken)
                .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Track resource
                aggregator.AddResource(resource);

                // Evaluate rules
                var resourceFindings = _ruleEngine.EvaluateRules(resource, config, showAnnotationValues)
                    .ToList();

                // Check if nginx-dependent
                var isNginxDependent = resourceFindings.Any(f => f.Category == "Detection");
                if (isNginxDependent)
                {
                    aggregator.MarkNginxDependent(resource.Namespace);
                }

                // Collect findings
                foreach (var finding in resourceFindings)
                {
                    findings.Add(finding);
                    aggregator.AddFinding(finding);
                }
            }

            // Collect warnings/errors from readers that support them
            if (reader is Adapters.Kubernetes.ClusterResourceReader clusterReader)
            {
                warnings.AddRange(clusterReader.Warnings);
            }
            else if (reader is Adapters.FileSystem.FileResourceReader fileReader)
            {
                warnings.AddRange(fileReader.Warnings);
                errors.AddRange(fileReader.Errors);
            }
            else if (reader is Adapters.FileSystem.StdinResourceReader stdinReader)
            {
                warnings.AddRange(stdinReader.Warnings);
                errors.AddRange(stdinReader.Errors);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Scan was cancelled");
            errors.Add("Scan was cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed with error: {Error}", ex.Message);
            errors.Add($"Scan failed: {ex.Message}");
        }

        stopwatch.Stop();

        // Evaluate policy
        var policy = _policyEvaluator.Evaluate(findings, config.Policy.FailOn);
        var exitCode = _policyEvaluator.DetermineExitCode(policy, warnings, errors, findings.Count > 0);

        // Update policy with correct exit code
        policy = new PolicyResult
        {
            Passed = policy.Passed,
            FailOnSeverity = policy.FailOnSeverity,
            ViolationCount = policy.ViolationCount,
            ExitCode = exitCode
        };

        // Determine status
        var status = errors.Count > 0 ? "incomplete" : "complete";

        // Create metadata
        var metadata = mode == ScanMode.Cluster
            ? ScanMetadata.ForClusterScan(DefaultConfiguration.ToolVersion, DefaultConfiguration.SchemaVersion, context, status)
            : ScanMetadata.ForRepoScan(DefaultConfiguration.ToolVersion, DefaultConfiguration.SchemaVersion, scannedPath, status);

        metadata = metadata with { Duration = stopwatch.Elapsed };

        // Sort findings for deterministic output
        var sortedFindings = findings
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.Resource.Namespace)
            .ThenBy(f => f.Resource.Name)
            .ThenBy(f => f.Id)
            .ToList();

        _logger.LogInformation(
            "Scan completed in {Duration:F2}s: {ResourceCount} resources, {FindingCount} findings",
            stopwatch.Elapsed.TotalSeconds,
            aggregator.GetSummary(policy).ResourcesScanned,
            findings.Count);

        return new ScanResult
        {
            Metadata = metadata,
            Summary = aggregator.GetSummary(policy),
            Findings = sortedFindings,
            Warnings = warnings,
            Errors = errors
        };
    }

    /// <summary>
    /// Generates reports from scan results.
    /// </summary>
    public async Task GenerateReportsAsync(
        ScanResult result,
        IEnumerable<string> formats,
        string? outputPath,
        CancellationToken cancellationToken = default)
    {
        var outputDir = outputPath ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        foreach (var format in formats)
        {
            var generator = _reportGenerators.FirstOrDefault(g =>
                g.Format.Equals(format, StringComparison.OrdinalIgnoreCase));

            if (generator == null)
            {
                _logger.LogWarning("Unknown report format: {Format}", format);
                continue;
            }

            var filePath = Path.Combine(outputDir, $"report{generator.FileExtension}");

            await using var stream = File.Create(filePath);
            await generator.GenerateAsync(result, stream, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Generated {Format} report: {Path}", format, filePath);
        }
    }

    /// <summary>
    /// Gets a report generator by format.
    /// </summary>
    public IReportGenerator? GetReportGenerator(string format) =>
        _reportGenerators.FirstOrDefault(g =>
            g.Format.Equals(format, StringComparison.OrdinalIgnoreCase));
}
