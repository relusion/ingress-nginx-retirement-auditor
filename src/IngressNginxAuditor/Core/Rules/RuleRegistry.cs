using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Rules;

/// <summary>
/// Registry for detection rules.
/// Manages rule registration, filtering by configuration, and metadata access.
/// </summary>
public class RuleRegistry
{
    private readonly Dictionary<string, IDetectionRule> _rules = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a detection rule.
    /// </summary>
    /// <param name="rule">The rule to register.</param>
    public void Register(IDetectionRule rule)
    {
        _rules[rule.RuleId] = rule;
    }

    /// <summary>
    /// Registers multiple detection rules.
    /// </summary>
    /// <param name="rules">The rules to register.</param>
    public void RegisterAll(IEnumerable<IDetectionRule> rules)
    {
        foreach (var rule in rules)
        {
            Register(rule);
        }
    }

    /// <summary>
    /// Gets all enabled rules based on configuration.
    /// </summary>
    /// <param name="enabledRuleIds">Explicit list of enabled rule IDs (null = all enabled by default).</param>
    /// <param name="disabledRuleIds">List of disabled rule IDs.</param>
    /// <param name="severityOverrides">Severity overrides by rule ID.</param>
    /// <returns>Enumerable of enabled rules with applied overrides.</returns>
    public IEnumerable<IDetectionRule> GetEnabledRules(
        IReadOnlySet<string>? enabledRuleIds = null,
        IReadOnlySet<string>? disabledRuleIds = null,
        IReadOnlyDictionary<string, Severity>? severityOverrides = null)
    {
        foreach (var rule in _rules.Values)
        {
            // Check if explicitly disabled
            if (disabledRuleIds?.Contains(rule.RuleId) == true)
                continue;

            // Check if enabled list is specified and rule is not in it
            if (enabledRuleIds != null && !enabledRuleIds.Contains(rule.RuleId))
                continue;

            // Apply severity override if specified
            if (rule is BaseDetectionRule baseRule && severityOverrides?.TryGetValue(rule.RuleId, out var severity) == true)
            {
                baseRule.EffectiveSeverity = severity;
            }

            yield return rule;
        }
    }

    /// <summary>
    /// Gets a specific rule by ID.
    /// </summary>
    /// <param name="ruleId">The rule ID to look up.</param>
    /// <returns>The rule if found, null otherwise.</returns>
    public IDetectionRule? GetRule(string ruleId) =>
        _rules.GetValueOrDefault(ruleId);

    /// <summary>
    /// Gets metadata for all registered rules.
    /// </summary>
    public IEnumerable<RuleMetadata> GetAllRuleMetadata() =>
        _rules.Values.Select(r => r.GetMetadata());

    /// <summary>
    /// Gets the count of registered rules.
    /// </summary>
    public int Count => _rules.Count;

    /// <summary>
    /// Gets all registered rule IDs.
    /// </summary>
    public IEnumerable<string> GetRuleIds() => _rules.Keys;
}
