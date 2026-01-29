using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Rules.Risk;

namespace IngressNginxAuditor.Tests.Rules.Risk;

public class SnippetRuleTests
{
    private readonly SnippetRule _rule = new();

    private static NormalizedResource CreateIngress(Dictionary<string, string>? annotations = null)
    {
        return new NormalizedResource
        {
            Kind = "Ingress",
            ApiVersion = "networking.k8s.io/v1",
            Name = "test-ingress",
            Namespace = "default",
            Labels = new Dictionary<string, string>(),
            Annotations = annotations ?? new Dictionary<string, string>()
        };
    }

    [Fact]
    public void Evaluate_WithConfigurationSnippet_ReturnsFinding()
    {
        // Arrange
        var resource = CreateIngress(new Dictionary<string, string>
        {
            ["nginx.ingress.kubernetes.io/configuration-snippet"] = "proxy_set_header X-Custom-Header $http_custom;"
        });

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().NotBeNull();
        finding!.Id.Should().Be("RISK-SNIPPET-001");
        finding.Severity.Should().Be(Severity.High);
        finding.Category.Should().Be("MigrationRisk");
    }

    [Fact]
    public void Evaluate_WithServerSnippet_ReturnsCriticalSeverity()
    {
        // Arrange
        var resource = CreateIngress(new Dictionary<string, string>
        {
            ["nginx.ingress.kubernetes.io/server-snippet"] = "add_header Strict-Transport-Security \"max-age=31536000\";"
        });

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().NotBeNull();
        finding!.Id.Should().Be("RISK-SNIPPET-001");
        finding.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void Evaluate_WithLocationSnippet_ReturnsFinding()
    {
        // Arrange
        var resource = CreateIngress(new Dictionary<string, string>
        {
            ["nginx.ingress.kubernetes.io/location-snippet"] = "proxy_cache my_cache;"
        });

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().NotBeNull();
        finding!.Id.Should().Be("RISK-SNIPPET-001");
    }

    [Fact]
    public void Evaluate_WithNoSnippetAnnotations_ReturnsNull()
    {
        // Arrange
        var resource = CreateIngress(new Dictionary<string, string>
        {
            ["nginx.ingress.kubernetes.io/ssl-redirect"] = "true"
        });

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WithServerSnippet_ReturnsCritical()
    {
        // Arrange
        var resource = CreateIngress(new Dictionary<string, string>
        {
            ["nginx.ingress.kubernetes.io/server-snippet"] = "some config"
        });

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().NotBeNull();
        finding!.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void Evaluate_WithConfigurationSnippet_ReturnsHigh()
    {
        // Arrange
        var resource = CreateIngress(new Dictionary<string, string>
        {
            ["nginx.ingress.kubernetes.io/configuration-snippet"] = "some config"
        });

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().NotBeNull();
        finding!.Severity.Should().Be(Severity.High);
    }

    [Fact]
    public void Evaluate_CollectsAllSnippetAnnotationsAsEvidence()
    {
        // Arrange
        var resource = CreateIngress(new Dictionary<string, string>
        {
            ["nginx.ingress.kubernetes.io/configuration-snippet"] = "config1",
            ["nginx.ingress.kubernetes.io/server-snippet"] = "config2"
        });

        // Act
        var finding = _rule.Evaluate(resource);

        // Assert
        finding.Should().NotBeNull();
        finding!.Evidence.Annotations.Should().HaveCount(2);
        // When both server-snippet and configuration-snippet are present, severity should be Critical
        finding.Severity.Should().Be(Severity.Critical);
    }
}
