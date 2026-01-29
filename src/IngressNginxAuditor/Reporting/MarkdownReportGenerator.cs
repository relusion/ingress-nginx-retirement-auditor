using System.Text;
using IngressNginxAuditor.Configuration;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Scoring;

namespace IngressNginxAuditor.Reporting;

/// <summary>
/// Generates Markdown reports from scan results.
/// </summary>
public class MarkdownReportGenerator : IReportGenerator
{
    public string Format => "md";
    public string FileExtension => ".md";

    public async Task GenerateAsync(
        ScanResult result,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        var content = GenerateReport(result);
        var bytes = Encoding.UTF8.GetBytes(content);
        await outputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public Task<string> GenerateToStringAsync(
        ScanResult result,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GenerateReport(result));
    }

    private static string GenerateReport(ScanResult result)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# Ingress NGINX Retirement Audit Report");
        sb.AppendLine();

        // Executive Summary
        GenerateExecutiveSummary(sb, result);

        // Warnings and Errors
        if (result.Warnings.Count > 0 || result.Errors.Count > 0)
        {
            GenerateWarningsSection(sb, result);
        }

        // Summary Statistics
        GenerateSummarySection(sb, result);

        // Findings by Severity
        if (result.Findings.Count > 0)
        {
            GenerateFindingsSection(sb, result);
        }

        // Namespace Breakdown
        if (result.Summary.ByNamespace.Count > 0)
        {
            GenerateNamespaceSection(sb, result);
        }

        // Recommendations
        GenerateRecommendationsSection(sb, result);

        // Metadata
        GenerateMetadataSection(sb, result);

        return sb.ToString();
    }

    private static void GenerateExecutiveSummary(StringBuilder sb, ScanResult result)
    {
        sb.AppendLine("## Executive Summary");
        sb.AppendLine();

        var summary = result.Summary;
        var status = result.Summary.Policy.Passed ? "PASS" : "FAIL";
        var statusEmoji = result.Summary.Policy.Passed ? "✅" : "❌";

        sb.AppendLine($"**Policy Status**: {statusEmoji} {status}");
        sb.AppendLine();

        sb.AppendLine($"- **Total Ingresses Scanned**: {summary.IngressesScanned}");
        sb.AppendLine($"- **NGINX-Dependent Ingresses**: {summary.NginxDependentIngresses}");
        sb.AppendLine($"- **Total Findings**: {summary.TotalFindings}");
        sb.AppendLine();

        // Risk breakdown
        if (summary.TotalFindings > 0)
        {
            sb.AppendLine("### Risk Distribution");
            sb.AppendLine();
            sb.AppendLine("| Severity | Count |");
            sb.AppendLine("|----------|-------|");

            foreach (var severity in Enum.GetValues<Severity>().Reverse())
            {
                var count = summary.FindingsBySeverity.GetValueOrDefault(severity, 0);
                if (count > 0)
                {
                    sb.AppendLine($"| {GetSeverityBadge(severity)} {severity} | {count} |");
                }
            }
            sb.AppendLine();
        }
    }

    private static void GenerateWarningsSection(StringBuilder sb, ScanResult result)
    {
        sb.AppendLine("## Scan Issues");
        sb.AppendLine();

        if (result.Errors.Count > 0)
        {
            sb.AppendLine("### Errors");
            foreach (var error in result.Errors)
            {
                sb.AppendLine($"- ❌ {error}");
            }
            sb.AppendLine();
        }

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine("### Warnings");
            foreach (var warning in result.Warnings)
            {
                sb.AppendLine($"- ⚠️ {warning}");
            }
            sb.AppendLine();
        }
    }

    private static void GenerateSummarySection(StringBuilder sb, ScanResult result)
    {
        sb.AppendLine("## Summary");
        sb.AppendLine();

        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Resources Scanned | {result.Summary.ResourcesScanned} |");
        sb.AppendLine($"| Ingresses Scanned | {result.Summary.IngressesScanned} |");
        sb.AppendLine($"| NGINX-Dependent | {result.Summary.NginxDependentIngresses} |");
        sb.AppendLine($"| Total Findings | {result.Summary.TotalFindings} |");
        sb.AppendLine($"| Policy Threshold | {result.Summary.Policy.FailOnSeverity} |");
        sb.AppendLine($"| Exit Code | {result.Summary.Policy.ExitCode} |");
        sb.AppendLine();
    }

    private static void GenerateFindingsSection(StringBuilder sb, ScanResult result)
    {
        sb.AppendLine("## Findings");
        sb.AppendLine();

        // Group by severity (descending)
        var groupedFindings = result.Findings
            .GroupBy(f => f.Severity)
            .OrderByDescending(g => g.Key);

        foreach (var group in groupedFindings)
        {
            sb.AppendLine($"### {GetSeverityBadge(group.Key)} {group.Key} ({group.Count()})");
            sb.AppendLine();

            foreach (var finding in group.OrderBy(f => f.Resource.Namespace).ThenBy(f => f.Resource.Name))
            {
                sb.AppendLine($"#### {finding.Id}: {finding.Title}");
                sb.AppendLine();
                sb.AppendLine($"**Resource**: `{finding.Resource}`");
                sb.AppendLine();
                sb.AppendLine($"**Message**: {finding.Message}");
                sb.AppendLine();

                // Evidence
                if (finding.Evidence.Annotations?.Count > 0)
                {
                    sb.AppendLine("**Annotations**:");
                    foreach (var (key, value) in finding.Evidence.Annotations)
                    {
                        var preview = value.TruncatedValue != null
                            ? $" (preview: `{value.TruncatedValue}`)"
                            : string.Empty;
                        sb.AppendLine($"- `{key}` ({value.ValueLength} chars, hash: `{value.ValueHashPrefix}`){preview}");
                    }
                    sb.AppendLine();
                }

                if (finding.Evidence.MatchedPatterns?.Count > 0)
                {
                    sb.AppendLine("**Matched Patterns**:");
                    foreach (var pattern in finding.Evidence.MatchedPatterns)
                    {
                        sb.AppendLine($"- `{pattern}`");
                    }
                    sb.AppendLine();
                }

                // Recommendations
                if (finding.Recommendations.Count > 0)
                {
                    sb.AppendLine("**Recommendations**:");
                    foreach (var rec in finding.Recommendations)
                    {
                        sb.AppendLine($"- {rec}");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }
        }
    }

    private static void GenerateNamespaceSection(StringBuilder sb, ScanResult result)
    {
        sb.AppendLine("## Namespace Breakdown");
        sb.AppendLine();

        sb.AppendLine("| Namespace | Ingresses | Findings | Risk Score |");
        sb.AppendLine("|-----------|-----------|----------|------------|");

        foreach (var (ns, rollup) in result.Summary.ByNamespace.OrderByDescending(kvp => kvp.Value.MaxRiskScore))
        {
            var riskLevel = RiskScorer.GetRiskLevel(rollup.MaxRiskScore);
            sb.AppendLine($"| {ns} | {rollup.IngressCount} | {rollup.FindingCount} | {rollup.MaxRiskScore} ({riskLevel}) |");
        }
        sb.AppendLine();
    }

    private static void GenerateRecommendationsSection(StringBuilder sb, ScanResult result)
    {
        sb.AppendLine("## Next Steps");
        sb.AppendLine();

        if (result.Summary.NginxDependentIngresses == 0)
        {
            sb.AppendLine("✅ No NGINX-dependent Ingress resources found. Your cluster may be ready for controller retirement.");
        }
        else
        {
            sb.AppendLine("The following steps are recommended to complete your migration:");
            sb.AppendLine();
            sb.AppendLine("1. **Review high-severity findings first** - Focus on Critical and High findings");
            sb.AppendLine("2. **Document snippet usage** - Custom NGINX configurations require manual translation");
            sb.AppendLine("3. **Test in staging** - Validate migration changes before production");
            sb.AppendLine("4. **Plan phased rollout** - Migrate namespace by namespace");
            sb.AppendLine("5. **Monitor during migration** - Watch for errors after each change");
        }
        sb.AppendLine();
    }

    private static void GenerateMetadataSection(StringBuilder sb, ScanResult result)
    {
        sb.AppendLine("## Report Metadata");
        sb.AppendLine();

        sb.AppendLine($"- **Tool**: {result.Metadata.Tool} v{result.Metadata.Version}");
        sb.AppendLine($"- **Scan Mode**: {result.Metadata.Mode}");
        sb.AppendLine($"- **Timestamp**: {result.Metadata.TimestampUtc:O}");
        sb.AppendLine($"- **Status**: {result.Metadata.Status}");

        if (!string.IsNullOrEmpty(result.Metadata.ClusterContext))
        {
            sb.AppendLine($"- **Cluster Context**: {result.Metadata.ClusterContext}");
        }

        if (!string.IsNullOrEmpty(result.Metadata.ScannedPath))
        {
            sb.AppendLine($"- **Scanned Path**: {result.Metadata.ScannedPath}");
        }

        if (result.Metadata.Duration.HasValue)
        {
            sb.AppendLine($"- **Duration**: {result.Metadata.Duration.Value.TotalSeconds:F2}s");
        }
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine($"*Generated by {DefaultConfiguration.ToolName} v{DefaultConfiguration.ToolVersion}*");
    }

    private static string GetSeverityBadge(Severity severity) => severity switch
    {
        Severity.Critical => "🔴",
        Severity.High => "🟠",
        Severity.Medium => "🟡",
        Severity.Low => "🔵",
        Severity.Info => "⚪",
        _ => "❓"
    };
}
