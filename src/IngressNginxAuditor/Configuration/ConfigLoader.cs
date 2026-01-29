using IngressNginxAuditor.Core.Models.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IngressNginxAuditor.Configuration;

/// <summary>
/// Loads configuration from YAML files and merges with defaults.
/// </summary>
public class ConfigLoader
{
    private readonly IDeserializer _deserializer;

    public ConfigLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Loads configuration from a file, merging with defaults.
    /// </summary>
    /// <param name="filePath">Path to the configuration file.</param>
    /// <returns>The merged configuration.</returns>
    public AuditorConfig LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}", filePath);
        }

        var yaml = File.ReadAllText(filePath);
        return LoadFromYaml(yaml);
    }

    /// <summary>
    /// Loads configuration from a YAML string, merging with defaults.
    /// </summary>
    /// <param name="yaml">YAML configuration content.</param>
    /// <returns>The merged configuration.</returns>
    public AuditorConfig LoadFromYaml(string yaml)
    {
        var rawConfig = _deserializer.Deserialize<RawConfig>(yaml) ?? new RawConfig();
        return MergeWithDefaults(rawConfig);
    }

    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static AuditorConfig GetDefault() => new();

    private static AuditorConfig MergeWithDefaults(RawConfig raw)
    {
        return new AuditorConfig
        {
            IngressClassNames = raw.IngressClassNames ?? DefaultConfiguration.IngressClassNames,
            AnnotationPrefixes = raw.AnnotationPrefixes ?? DefaultConfiguration.AnnotationPrefixes,
            Rules = MergeRulesConfig(raw.Rules),
            Policy = MergePolicyConfig(raw.Policy),
            Output = MergeOutputConfig(raw.Output),
            Cluster = MergeClusterConfig(raw.Cluster)
        };
    }

    private static RulesConfig MergeRulesConfig(RawRulesConfig? raw)
    {
        if (raw == null) return new RulesConfig();

        return new RulesConfig
        {
            Enabled = raw.Enabled?.ToHashSet(StringComparer.OrdinalIgnoreCase),
            Disabled = raw.Disabled?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [],
            SeverityOverrides = ParseSeverityOverrides(raw.SeverityOverrides)
        };
    }

    private static Dictionary<string, Severity> ParseSeverityOverrides(Dictionary<string, string>? raw)
    {
        if (raw == null) return [];

        var result = new Dictionary<string, Severity>(StringComparer.OrdinalIgnoreCase);
        foreach (var (ruleId, severityStr) in raw)
        {
            if (Enum.TryParse<Severity>(severityStr, ignoreCase: true, out var severity))
            {
                result[ruleId] = severity;
            }
        }
        return result;
    }

    private static PolicyConfig MergePolicyConfig(RawPolicyConfig? raw)
    {
        if (raw == null) return new PolicyConfig();

        var failOn = Severity.High;
        if (!string.IsNullOrEmpty(raw.FailOn) &&
            Enum.TryParse<Severity>(raw.FailOn, ignoreCase: true, out var parsed))
        {
            failOn = parsed;
        }

        return new PolicyConfig { FailOn = failOn };
    }

    private static OutputConfig MergeOutputConfig(RawOutputConfig? raw)
    {
        if (raw == null) return new OutputConfig();

        return new OutputConfig
        {
            Formats = raw.Formats ?? DefaultConfiguration.OutputFormats,
            OutputPath = raw.OutputPath,
            ShowAnnotationValues = raw.ShowAnnotationValues ?? false
        };
    }

    private static ClusterConfig MergeClusterConfig(RawClusterConfig? raw)
    {
        if (raw == null) return new ClusterConfig();

        return new ClusterConfig
        {
            ApiConcurrency = raw.ApiConcurrency ?? DefaultConfiguration.ApiConcurrency,
            TimeoutSeconds = raw.TimeoutSeconds ?? DefaultConfiguration.TimeoutSeconds
        };
    }

    // Raw config classes for YAML deserialization
    private sealed class RawConfig
    {
        public List<string>? IngressClassNames { get; set; }
        public List<string>? AnnotationPrefixes { get; set; }
        public RawRulesConfig? Rules { get; set; }
        public RawPolicyConfig? Policy { get; set; }
        public RawOutputConfig? Output { get; set; }
        public RawClusterConfig? Cluster { get; set; }
    }

    private sealed class RawRulesConfig
    {
        public List<string>? Enabled { get; set; }
        public List<string>? Disabled { get; set; }
        public Dictionary<string, string>? SeverityOverrides { get; set; }
    }

    private sealed class RawPolicyConfig
    {
        public string? FailOn { get; set; }
    }

    private sealed class RawOutputConfig
    {
        public List<string>? Formats { get; set; }
        public string? OutputPath { get; set; }
        public bool? ShowAnnotationValues { get; set; }
    }

    private sealed class RawClusterConfig
    {
        public int? ApiConcurrency { get; set; }
        public int? TimeoutSeconds { get; set; }
    }
}
