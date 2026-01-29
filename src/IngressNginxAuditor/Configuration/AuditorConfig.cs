using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Configuration;

/// <summary>
/// Configuration for the ingress-nginx-auditor tool.
/// </summary>
public sealed record AuditorConfig
{
    /// <summary>
    /// IngressClassName values that identify ingress-nginx.
    /// </summary>
    public IReadOnlyList<string> IngressClassNames { get; init; } = DefaultConfiguration.IngressClassNames;

    /// <summary>
    /// Annotation prefixes that identify ingress-nginx.
    /// </summary>
    public IReadOnlyList<string> AnnotationPrefixes { get; init; } = DefaultConfiguration.AnnotationPrefixes;

    /// <summary>
    /// Rule configuration settings.
    /// </summary>
    public RulesConfig Rules { get; init; } = new();

    /// <summary>
    /// Policy configuration settings.
    /// </summary>
    public PolicyConfig Policy { get; init; } = new();

    /// <summary>
    /// Output configuration settings.
    /// </summary>
    public OutputConfig Output { get; init; } = new();

    /// <summary>
    /// Cluster configuration settings.
    /// </summary>
    public ClusterConfig Cluster { get; init; } = new();
}

/// <summary>
/// Rule-specific configuration.
/// </summary>
public sealed record RulesConfig
{
    /// <summary>
    /// Explicit list of enabled rule IDs (null = all enabled by default).
    /// </summary>
    public HashSet<string>? Enabled { get; init; }

    /// <summary>
    /// List of disabled rule IDs.
    /// </summary>
    public HashSet<string> Disabled { get; init; } = [];

    /// <summary>
    /// Severity overrides by rule ID.
    /// </summary>
    public Dictionary<string, Severity> SeverityOverrides { get; init; } = [];
}

/// <summary>
/// Policy evaluation configuration.
/// </summary>
public sealed record PolicyConfig
{
    /// <summary>
    /// Minimum severity level that triggers a policy failure (exit code 1).
    /// </summary>
    public Severity FailOn { get; init; } = Severity.High;
}

/// <summary>
/// Output configuration.
/// </summary>
public sealed record OutputConfig
{
    /// <summary>
    /// Output formats to generate.
    /// </summary>
    public IReadOnlyList<string> Formats { get; init; } = DefaultConfiguration.OutputFormats;

    /// <summary>
    /// Output directory path.
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>
    /// Whether to show annotation values in evidence.
    /// </summary>
    public bool ShowAnnotationValues { get; init; } = false;
}

/// <summary>
/// Cluster-specific configuration.
/// </summary>
public sealed record ClusterConfig
{
    /// <summary>
    /// Maximum concurrent Kubernetes API calls.
    /// </summary>
    public int ApiConcurrency { get; init; } = DefaultConfiguration.ApiConcurrency;

    /// <summary>
    /// Operation timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = DefaultConfiguration.TimeoutSeconds;
}
