using FluentAssertions;
using IngressNginxAuditor.Adapters.FileSystem;
using IngressNginxAuditor.Configuration;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Infrastructure;
using IngressNginxAuditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IngressNginxAuditor.Tests.Integration;

public class RepoScanIntegrationTests
{
    private readonly string _fixturesPath;

    public RepoScanIntegrationTests()
    {
        // Find fixtures relative to test assembly
        var assemblyDir = Path.GetDirectoryName(typeof(RepoScanIntegrationTests).Assembly.Location)!;
        _fixturesPath = Path.Combine(assemblyDir, "..", "..", "..", "Fixtures", "Manifests");

        // Normalize path
        _fixturesPath = Path.GetFullPath(_fixturesPath);
    }

    [Fact]
    public async Task ScanAsync_WithFixtureManifests_DetectsAllNginxIngresses()
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

        // Act
        var result = await orchestrator.ScanAsync(
            reader,
            options,
            config,
            ScanMode.Repo,
            scannedPath: _fixturesPath);

        // Assert
        result.Should().NotBeNull();
        result.Summary.ResourcesScanned.Should().BeGreaterThan(0);
        result.Summary.IngressesScanned.Should().BeGreaterThan(0);
        result.Summary.NginxDependentIngresses.Should().BeGreaterThan(0);
        result.Summary.TotalFindings.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ScanAsync_WithNginxClassManifest_DetectsIngressClass()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault();

        var singleFilePath = Path.Combine(_fixturesPath, "nginx-class.yaml");
        var options = new ResourceReaderOptions
        {
            Path = Path.GetDirectoryName(singleFilePath),
            GlobPatterns = [Path.GetFileName(singleFilePath)]
        };

        // Act
        var result = await orchestrator.ScanAsync(
            reader,
            options,
            config,
            ScanMode.Repo,
            scannedPath: singleFilePath);

        // Assert
        result.Findings.Should().Contain(f => f.Id == "DET-NGINX-CLASS-001");
    }

    [Fact]
    public async Task ScanAsync_WithSnippetManifest_DetectsHighRisk()
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

        // Act
        var result = await orchestrator.ScanAsync(
            reader,
            options,
            config,
            ScanMode.Repo,
            scannedPath: singleFilePath);

        // Assert
        result.Findings.Should().Contain(f =>
            f.Id == "RISK-SNIPPET-001" &&
            f.Severity >= Severity.High);
    }

    [Fact]
    public async Task ScanAsync_WithServerSnippetManifest_DetectsCriticalRisk()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault();

        var singleFilePath = Path.Combine(_fixturesPath, "snippet-critical-risk.yaml");
        var options = new ResourceReaderOptions
        {
            Path = Path.GetDirectoryName(singleFilePath),
            GlobPatterns = [Path.GetFileName(singleFilePath)]
        };

        // Act
        var result = await orchestrator.ScanAsync(
            reader,
            options,
            config,
            ScanMode.Repo,
            scannedPath: singleFilePath);

        // Assert
        result.Findings.Should().Contain(f =>
            f.Id == "RISK-SNIPPET-001" &&
            f.Severity == Severity.Critical);
    }

    [Fact]
    public async Task ScanAsync_WithNonNginxManifest_ProducesNoNginxFindings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault();

        var singleFilePath = Path.Combine(_fixturesPath, "non-nginx.yaml");
        var options = new ResourceReaderOptions
        {
            Path = Path.GetDirectoryName(singleFilePath),
            GlobPatterns = [Path.GetFileName(singleFilePath)]
        };

        // Act
        var result = await orchestrator.ScanAsync(
            reader,
            options,
            config,
            ScanMode.Repo,
            scannedPath: singleFilePath);

        // Assert
        result.Summary.NginxDependentIngresses.Should().Be(0);
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithMultiDocumentYaml_ParsesAllDocuments()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault();

        var singleFilePath = Path.Combine(_fixturesPath, "multi-document.yaml");
        var options = new ResourceReaderOptions
        {
            Path = Path.GetDirectoryName(singleFilePath),
            GlobPatterns = [Path.GetFileName(singleFilePath)]
        };

        // Act
        var result = await orchestrator.ScanAsync(
            reader,
            options,
            config,
            ScanMode.Repo,
            scannedPath: singleFilePath);

        // Assert - should find 2 Ingresses (ConfigMap is ignored)
        result.Summary.IngressesScanned.Should().Be(2);
        result.Summary.NginxDependentIngresses.Should().Be(2);
    }

    [Fact]
    public async Task ScanAsync_WithComplexManifest_DetectsMultipleRules()
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

        // Act
        var result = await orchestrator.ScanAsync(
            reader,
            options,
            config,
            ScanMode.Repo,
            scannedPath: singleFilePath);

        // Assert - should trigger multiple rules
        var ruleIds = result.Findings.Select(f => f.Id).Distinct().ToList();
        ruleIds.Should().Contain("DET-NGINX-CLASS-001");
        ruleIds.Should().Contain("DET-NGINX-ANNOT-PREFIX-001");
        ruleIds.Should().Contain("RISK-SNIPPET-001");
        ruleIds.Should().Contain("RISK-REWRITE-001");
        ruleIds.Should().Contain("RISK-REGEX-001");
        ruleIds.Should().Contain("RISK-AUTH-001");
    }

    [Fact]
    public async Task ScanAsync_ProducesCorrectNamespaceRollup()
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

        // Act
        var result = await orchestrator.ScanAsync(
            reader,
            options,
            config,
            ScanMode.Repo,
            scannedPath: _fixturesPath);

        // Assert
        result.Summary.ByNamespace.Should().NotBeEmpty();
        result.Summary.ByNamespace.Should().ContainKey("production");
    }
}
