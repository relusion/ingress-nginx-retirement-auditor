using FluentAssertions;
using IngressNginxAuditor.Adapters.FileSystem;
using IngressNginxAuditor.Configuration;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Infrastructure;
using IngressNginxAuditor.Reporting;
using IngressNginxAuditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IngressNginxAuditor.Tests.Integration;

public class DeterminismTests
{
    private readonly string _fixturesPath;

    public DeterminismTests()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(DeterminismTests).Assembly.Location)!;
        _fixturesPath = Path.Combine(assemblyDir, "..", "..", "..", "Fixtures", "Manifests");
        _fixturesPath = Path.GetFullPath(_fixturesPath);
    }

    [Fact]
    public async Task JsonReport_SameInput_ProducesDeterministicOutput()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault();

        var singleFilePath = Path.Combine(_fixturesPath, "complex-multi-rule.yaml");
        var options = new ResourceReaderOptions
        {
            Path = Path.GetDirectoryName(singleFilePath),
            GlobPatterns = [Path.GetFileName(singleFilePath)]
        };

        // Act - Run scan twice
        var result1 = await orchestrator.ScanAsync(
            reader, options, config, ScanMode.Repo, scannedPath: singleFilePath);

        var result2 = await orchestrator.ScanAsync(
            reader, options, config, ScanMode.Repo, scannedPath: singleFilePath);

        // Generate JSON for both
        var jsonGenerator = new JsonReportGenerator();
        var json1 = await jsonGenerator.GenerateToStringAsync(result1);
        var json2 = await jsonGenerator.GenerateToStringAsync(result2);

        // Assert - Remove timestamp-related fields for comparison
        var normalized1 = NormalizeJsonForComparison(json1);
        var normalized2 = NormalizeJsonForComparison(json2);

        normalized1.Should().Be(normalized2,
            "Same input should produce identical output (excluding timestamps)");
    }

    [Fact]
    public async Task FindingsOrder_IsDeterministic()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault();

        var options = new ResourceReaderOptions
        {
            Path = _fixturesPath,
            GlobPatterns = ["**/*.yaml", "**/*.yml"]
        };

        // Act - Run scan multiple times
        var results = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var result = await orchestrator.ScanAsync(
                reader, options, config, ScanMode.Repo, scannedPath: _fixturesPath);

            // Create a string representation of finding IDs and resources
            var findingsSignature = string.Join("|",
                result.Findings.Select(f => $"{f.Id}:{f.Resource.Namespace}/{f.Resource.Name}"));

            results.Add(findingsSignature);
        }

        // Assert - All runs should have same order
        results.Should().AllBeEquivalentTo(results[0],
            "Findings order should be deterministic across runs");
    }

    [Fact]
    public async Task MarkdownReport_SameInput_ProducesDeterministicStructure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault();

        var singleFilePath = Path.Combine(_fixturesPath, "complex-multi-rule.yaml");
        var options = new ResourceReaderOptions
        {
            Path = Path.GetDirectoryName(singleFilePath),
            GlobPatterns = [Path.GetFileName(singleFilePath)]
        };

        // Act - Run scan twice
        var result1 = await orchestrator.ScanAsync(
            reader, options, config, ScanMode.Repo, scannedPath: singleFilePath);

        var result2 = await orchestrator.ScanAsync(
            reader, options, config, ScanMode.Repo, scannedPath: singleFilePath);

        // Generate Markdown for both
        var mdGenerator = new MarkdownReportGenerator();
        var md1 = await mdGenerator.GenerateToStringAsync(result1);
        var md2 = await mdGenerator.GenerateToStringAsync(result2);

        // Assert - Remove timestamp lines for comparison
        var normalized1 = NormalizeMarkdownForComparison(md1);
        var normalized2 = NormalizeMarkdownForComparison(md2);

        normalized1.Should().Be(normalized2,
            "Same input should produce identical markdown structure (excluding timestamps)");
    }

    [Fact]
    public async Task HashPrefix_IsDeterministic()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault();

        var singleFilePath = Path.Combine(_fixturesPath, "snippet-high-risk.yaml");
        var options = new ResourceReaderOptions
        {
            Path = Path.GetDirectoryName(singleFilePath),
            GlobPatterns = [Path.GetFileName(singleFilePath)]
        };

        // Act - Run scan twice
        var result1 = await orchestrator.ScanAsync(
            reader, options, config, ScanMode.Repo, scannedPath: singleFilePath);

        var result2 = await orchestrator.ScanAsync(
            reader, options, config, ScanMode.Repo, scannedPath: singleFilePath);

        // Extract hash prefixes from findings
        var hashes1 = result1.Findings
            .Where(f => f.Evidence.Annotations != null)
            .SelectMany(f => f.Evidence.Annotations!.Values.Select(a => a.ValueHashPrefix))
            .ToList();

        var hashes2 = result2.Findings
            .Where(f => f.Evidence.Annotations != null)
            .SelectMany(f => f.Evidence.Annotations!.Values.Select(a => a.ValueHashPrefix))
            .ToList();

        // Assert
        hashes1.Should().BeEquivalentTo(hashes2,
            "Hash prefixes should be deterministic for same content");
    }

    private static string NormalizeJsonForComparison(string json)
    {
        // Remove fields that vary between runs
        var lines = json.Split('\n')
            .Where(l => !l.Contains("\"timestampUtc\""))
            .Where(l => !l.Contains("\"durationMs\""));

        return string.Join('\n', lines);
    }

    private static string NormalizeMarkdownForComparison(string markdown)
    {
        // Remove lines that vary between runs
        var lines = markdown.Split('\n')
            .Where(l => !l.Contains("**Timestamp**"))
            .Where(l => !l.Contains("**Duration**"));

        return string.Join('\n', lines);
    }
}
