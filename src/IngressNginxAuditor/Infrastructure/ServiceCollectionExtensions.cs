using IngressNginxAuditor.Adapters.FileSystem;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Rules;
using IngressNginxAuditor.Core.Scoring;
using IngressNginxAuditor.Reporting;
using IngressNginxAuditor.Rules.Detection;
using IngressNginxAuditor.Rules.Risk;
using IngressNginxAuditor.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IngressNginxAuditor.Infrastructure;

/// <summary>
/// Extension methods for registering services with DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all auditor services to the service collection.
    /// </summary>
    public static IServiceCollection AddAuditorServices(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<RiskScorer>();
        services.AddSingleton<RuleRegistry>(sp =>
        {
            var registry = new RuleRegistry();
            RegisterAllRules(registry);
            return registry;
        });

        // Application services
        services.AddSingleton<RuleEngine>();
        services.AddSingleton<PolicyEvaluator>();
        services.AddSingleton<ScanOrchestrator>();

        // Report generators
        services.AddSingleton<IReportGenerator, MarkdownReportGenerator>();
        services.AddSingleton<IReportGenerator, JsonReportGenerator>();

        // FileSystem adapters
        services.AddTransient<GlobMatcher>();
        services.AddTransient<YamlDocumentParser>();

        return services;
    }

    /// <summary>
    /// Registers all detection and risk rules with the registry.
    /// </summary>
    public static void RegisterAllRules(RuleRegistry registry)
    {
        // Detection rules
        registry.Register(new NginxClassRule());
        registry.Register(new AnnotationPrefixRule());
        registry.Register(new ControllerDetectionRule());

        // Risk rules
        registry.Register(new SnippetRule());
        registry.Register(new RegexRule());
        registry.Register(new RewriteRule());
        registry.Register(new AuthRule());
        registry.Register(new TlsRedirectRule());
    }
}
