using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Scoring;

namespace IngressNginxAuditor.Tests.Core;

public class RiskScorerTests
{
    private readonly RiskScorer _scorer = new();

    private static Finding CreateFinding(Severity severity, Confidence confidence = Confidence.High)
    {
        return new Finding
        {
            Id = "TEST-001",
            Title = "Test Finding",
            Severity = severity,
            Confidence = confidence,
            Category = "Test",
            Resource = new ResourceReference
            {
                Kind = "Ingress",
                ApiVersion = "networking.k8s.io/v1",
                Name = "test",
                Namespace = "default"
            },
            Evidence = Evidence.Empty,
            Message = "Test message",
            Recommendations = []
        };
    }

    [Fact]
    public void CalculateScore_WithNoFindings_ReturnsZero()
    {
        // Arrange
        var findings = Array.Empty<Finding>();

        // Act
        var score = _scorer.CalculateScore(findings);

        // Assert
        score.Should().Be(0);
    }

    [Fact]
    public void CalculateScore_WithSingleCriticalFinding_Returns40()
    {
        // Arrange
        var findings = new[] { CreateFinding(Severity.Critical) };

        // Act
        var score = _scorer.CalculateScore(findings);

        // Assert
        score.Should().Be(40);
    }

    [Fact]
    public void CalculateScore_WithSingleHighFinding_Returns25()
    {
        // Arrange
        var findings = new[] { CreateFinding(Severity.High) };

        // Act
        var score = _scorer.CalculateScore(findings);

        // Assert
        score.Should().Be(25);
    }

    [Fact]
    public void CalculateScore_WithSingleMediumFinding_Returns10()
    {
        // Arrange
        var findings = new[] { CreateFinding(Severity.Medium) };

        // Act
        var score = _scorer.CalculateScore(findings);

        // Assert
        score.Should().Be(10);
    }

    [Fact]
    public void CalculateScore_WithSingleLowFinding_Returns5()
    {
        // Arrange
        var findings = new[] { CreateFinding(Severity.Low) };

        // Act
        var score = _scorer.CalculateScore(findings);

        // Assert
        score.Should().Be(5);
    }

    [Fact]
    public void CalculateScore_WithSingleInfoFinding_Returns1()
    {
        // Arrange
        var findings = new[] { CreateFinding(Severity.Info) };

        // Act
        var score = _scorer.CalculateScore(findings);

        // Assert
        score.Should().Be(1);
    }

    [Fact]
    public void CalculateScore_WithMultipleFindings_SumsWeights()
    {
        // Arrange
        var findings = new[]
        {
            CreateFinding(Severity.High),   // 25
            CreateFinding(Severity.Medium), // 10
            CreateFinding(Severity.Low)     // 5
        };

        // Act
        var score = _scorer.CalculateScore(findings);

        // Assert
        score.Should().Be(40); // 25 + 10 + 5
    }

    [Fact]
    public void CalculateScore_CapsAt100()
    {
        // Arrange - Create enough findings to exceed 100
        var findings = new[]
        {
            CreateFinding(Severity.Critical), // 40
            CreateFinding(Severity.Critical), // 40
            CreateFinding(Severity.Critical)  // 40 = 120, but should cap at 100
        };

        // Act
        var score = _scorer.CalculateScore(findings);

        // Assert
        score.Should().Be(100);
    }

    [Theory]
    [InlineData(0, "Minimal")]
    [InlineData(9, "Minimal")]
    [InlineData(10, "Low")]
    [InlineData(24, "Low")]
    [InlineData(25, "Medium")]
    [InlineData(49, "Medium")]
    [InlineData(50, "High")]
    [InlineData(79, "High")]
    [InlineData(80, "Critical")]
    [InlineData(100, "Critical")]
    public void GetRiskLevel_ReturnsCorrectLevel(int score, string expectedLevel)
    {
        // Act
        var level = RiskScorer.GetRiskLevel(score);

        // Assert
        level.Should().Be(expectedLevel);
    }
}
