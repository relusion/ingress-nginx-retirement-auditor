using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Rules.Detection;

namespace IngressNginxAuditor.Tests.Rules.Detection;

public class NginxClassRuleTests
{
    private readonly NginxClassRule _rule = new();

    private static NormalizedResource CreateIngress(
        string? ingressClassName = null,
        Dictionary<string, string>? annotations = null)
    {
        return new NormalizedResource
        {
            Kind = "Ingress",
            ApiVersion = "networking.k8s.io/v1",
            Name = "test-ingress",
            Namespace = "default",
            Labels = new Dictionary<string, string>(),
            Annotations = annotations ?? new Dictionary<string, string>(),
            IngressClassName = ingressClassName
        };
    }

    [Fact]
    public void Evaluate_WithNginxIngressClassName_ReturnsFinding()
    {
        // Arrange
        var resource = CreateIngress(ingressClassName: "nginx");

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().NotBeNull();
        finding!.Id.Should().Be("DET-NGINX-CLASS-001");
        finding.Severity.Should().Be(Severity.Info);
        finding.Confidence.Should().Be(Confidence.High);
        finding.Category.Should().Be("Detection");
    }

    [Fact]
    public void Evaluate_WithLegacyNginxAnnotation_ReturnsFinding()
    {
        // Arrange
        var resource = CreateIngress(annotations: new Dictionary<string, string>
        {
            ["kubernetes.io/ingress.class"] = "nginx"
        });

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().NotBeNull();
        finding!.Id.Should().Be("DET-NGINX-CLASS-001");
    }

    [Fact]
    public void Evaluate_WithNonNginxClassName_ReturnsNull()
    {
        // Arrange
        var resource = CreateIngress(ingressClassName: "traefik");

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WithNoClassName_ReturnsNull()
    {
        // Arrange
        var resource = CreateIngress();

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WithNonIngressKind_ReturnsNull()
    {
        // Arrange
        var resource = new NormalizedResource
        {
            Kind = "Deployment",
            ApiVersion = "apps/v1",
            Name = "test-deployment",
            Namespace = "default",
            Labels = new Dictionary<string, string>(),
            Annotations = new Dictionary<string, string>(),
            IngressClassName = "nginx" // Even with nginx class, should not match
        };

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().BeNull();
    }

    [Theory]
    [InlineData("nginx")]
    [InlineData("NGINX")]
    [InlineData("Nginx")]
    public void Evaluate_IsCaseInsensitive(string className)
    {
        // Arrange
        var resource = CreateIngress(ingressClassName: className);

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().NotBeNull();
    }

    [Fact]
    public void GetMetadata_ReturnsCorrectMetadata()
    {
        // Act
        var metadata = _rule.GetMetadata();

        // Assert
        metadata.RuleId.Should().Be("DET-NGINX-CLASS-001");
        metadata.Title.Should().Be("NGINX IngressClass detected");
        metadata.Category.Should().Be("Detection");
        metadata.DefaultSeverity.Should().Be(Severity.Info);
        metadata.Recommendations.Should().NotBeEmpty();
    }
}
