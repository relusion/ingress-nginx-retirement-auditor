namespace IngressNginxAuditor.Configuration;

/// <summary>
/// Default configuration values for the auditor.
/// </summary>
public static class DefaultConfiguration
{
    /// <summary>
    /// Default IngressClassName values that identify ingress-nginx.
    /// </summary>
    public static readonly IReadOnlyList<string> IngressClassNames = new[]
    {
        "nginx",
        "nginx-internal",
        "nginx-external"
    };

    /// <summary>
    /// Default annotation prefixes that identify ingress-nginx.
    /// </summary>
    public static readonly IReadOnlyList<string> AnnotationPrefixes = new[]
    {
        "nginx.ingress.kubernetes.io/",
        "nginx.org/"
    };

    /// <summary>
    /// Default output formats.
    /// </summary>
    public static readonly IReadOnlyList<string> OutputFormats = new[] { "md", "json" };

    /// <summary>
    /// Default maximum concurrent Kubernetes API calls.
    /// </summary>
    public const int ApiConcurrency = 10;

    /// <summary>
    /// Default operation timeout in seconds.
    /// </summary>
    public const int TimeoutSeconds = 30;

    /// <summary>
    /// Default glob patterns for YAML files.
    /// </summary>
    public static readonly IReadOnlyList<string> YamlGlobPatterns = new[]
    {
        "**/*.yaml",
        "**/*.yml"
    };

    /// <summary>
    /// Default exclude patterns for file scanning.
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludePatterns = new[]
    {
        "**/node_modules/**",
        "**/.git/**",
        "**/vendor/**"
    };

    /// <summary>
    /// Legacy ingress class annotation key.
    /// </summary>
    public const string LegacyIngressClassAnnotation = "kubernetes.io/ingress.class";

    /// <summary>
    /// Tool name for metadata.
    /// </summary>
    public const string ToolName = "ingress-nginx-auditor";

    /// <summary>
    /// Current tool version.
    /// </summary>
    public const string ToolVersion = "1.0.0";

    /// <summary>
    /// JSON schema version for output.
    /// </summary>
    public const string SchemaVersion = "1.0";
}
