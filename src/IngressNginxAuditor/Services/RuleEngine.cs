using IngressNginxAuditor.Configuration;
using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngressNginxAuditor.Services;

/// <summary>
/// Executes detection rules against resources and collects findings.
/// </summary>
public class RuleEngine
{
    private readonly RuleRegistry _registry;
    private readonly ILogger<RuleEngine> _logger;

    public RuleEngine(RuleRegistry registry, ILogger<RuleEngine>? logger = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? NullLogger<RuleEngine>.Instance;
    }

    /// <summary>
    /// Evaluates all enabled rules against a resource.
    /// </summary>
    /// <param name="resource">The resource to evaluate.</param>
    /// <param name="config">Configuration for rule filtering and overrides.</param>
    /// <param name="showAnnotationValues">Whether to include annotation value previews.</param>
    /// <returns>All findings from matching rules.</returns>
    public IEnumerable<Finding> EvaluateRules(
        NormalizedResource resource,
        AuditorConfig config,
        bool showAnnotationValues = false)
    {
        var enabledRules = _registry.GetEnabledRules(
            config.Rules.Enabled,
            config.Rules.Disabled,
            config.Rules.SeverityOverrides);

        foreach (var rule in enabledRules)
        {
            Finding? finding = null;

            try
            {
                finding = rule.Evaluate(resource, showAnnotationValues);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Rule {RuleId} failed on resource {Resource}: {Error}",
                    rule.RuleId, resource, ex.Message);
            }

            if (finding != null)
            {
                _logger.LogDebug(
                    "Rule {RuleId} matched resource {Resource} with severity {Severity}",
                    rule.RuleId, resource, finding.Severity);

                yield return finding;
            }
        }
    }

    /// <summary>
    /// Checks if a resource is NGINX-dependent based on detection rules.
    /// </summary>
    public bool IsNginxDependent(NormalizedResource resource, AuditorConfig config)
    {
        var findings = EvaluateRules(resource, config);
        return findings.Any(f => f.Category == "Detection");
    }

    /// <summary>
    /// Gets all registered rule metadata.
    /// </summary>
    public IEnumerable<RuleMetadata> GetAllRuleMetadata() =>
        _registry.GetAllRuleMetadata();

    /// <summary>
    /// Gets metadata for a specific rule.
    /// </summary>
    public RuleMetadata? GetRuleMetadata(string ruleId)
    {
        var rule = _registry.GetRule(ruleId);
        return rule?.GetMetadata();
    }
}
