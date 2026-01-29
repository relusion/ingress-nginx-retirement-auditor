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

public class ExitCodeTests
{
    private readonly string _fixturesPath;

    public ExitCodeTests()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(ExitCodeTests).Assembly.Location)!;
        _fixturesPath = Path.Combine(assemblyDir, "..", "..", "..", "Fixtures", "Manifests");
        _fixturesPath = Path.GetFullPath(_fixturesPath);
    }

    [Fact]
    public async Task ExitCode_WithNoFindings_ReturnsSuccess()
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
        result.Summary.Policy.ExitCode.Should().Be(ExitCodes.Success);
        result.Summary.Policy.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ExitCode_WithHighFindingsAndHighThreshold_ReturnsPolicyViolation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault() with
        {
            Policy = new PolicyConfig { FailOn = Severity.High }
        };

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
        result.Summary.Policy.ExitCode.Should().Be(ExitCodes.PolicyViolation);
        result.Summary.Policy.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ExitCode_WithMediumFindingsAndHighThreshold_ReturnsSuccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault() with
        {
            Policy = new PolicyConfig { FailOn = Severity.High }
        };

        // Use TLS redirect which produces LOW/MEDIUM findings only
        var singleFilePath = Path.Combine(_fixturesPath, "tls-redirect.yaml");
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

        // Assert - should pass because highest severity is MEDIUM, threshold is HIGH
        result.Summary.Policy.ExitCode.Should().Be(ExitCodes.Success);
        result.Summary.Policy.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ExitCode_WithMediumFindingsAndMediumThreshold_ReturnsPolicyViolation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault() with
        {
            Policy = new PolicyConfig { FailOn = Severity.Medium }
        };

        var singleFilePath = Path.Combine(_fixturesPath, "regex-rewrite.yaml");
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
        result.Summary.Policy.ExitCode.Should().Be(ExitCodes.PolicyViolation);
        result.Summary.Policy.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ExitCode_WithCriticalFindingsAndCriticalThreshold_ReturnsPolicyViolation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault() with
        {
            Policy = new PolicyConfig { FailOn = Severity.Critical }
        };

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
        result.Summary.Policy.ExitCode.Should().Be(ExitCodes.PolicyViolation);
        result.Summary.Policy.Passed.Should().BeFalse();
        result.Summary.Policy.ViolationCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExitCode_WithInfoFindingsAndInfoThreshold_ReturnsPolicyViolation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAuditorServices();
        await using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ScanOrchestrator>();
        var reader = new FileResourceReader();
        var config = ConfigLoader.GetDefault() with
        {
            Policy = new PolicyConfig { FailOn = Severity.Info }
        };

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

        // Assert - Info findings with Info threshold should fail
        result.Summary.Policy.ExitCode.Should().Be(ExitCodes.PolicyViolation);
        result.Summary.Policy.Passed.Should().BeFalse();
    }
}
