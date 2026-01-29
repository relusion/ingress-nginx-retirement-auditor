using System.Security.Cryptography;
using System.Text;

namespace IngressNginxAuditor.Core.Models;

/// <summary>
/// Represents an annotation with its value redacted for security.
/// Provides metadata about the annotation without exposing sensitive content.
/// </summary>
public sealed record RedactedAnnotation
{
    /// <summary>The annotation key name.</summary>
    public required string Key { get; init; }

    /// <summary>The length of the annotation value in characters.</summary>
    public required int ValueLength { get; init; }

    /// <summary>First 8 characters of the SHA256 hash of the value (for change detection).</summary>
    public required string ValueHashPrefix { get; init; }

    /// <summary>Truncated value preview (only populated when --show-annotation-values is used).</summary>
    public string? TruncatedValue { get; init; }

    /// <summary>
    /// Creates a RedactedAnnotation from a key-value pair.
    /// </summary>
    /// <param name="key">The annotation key.</param>
    /// <param name="value">The annotation value to redact.</param>
    /// <param name="showValues">Whether to include a truncated value preview.</param>
    /// <param name="maxPreviewLength">Maximum length for the truncated value preview.</param>
    public static RedactedAnnotation FromAnnotation(
        string key,
        string value,
        bool showValues = false,
        int maxPreviewLength = 50)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var hashPrefix = Convert.ToHexString(hashBytes)[..8];

        return new RedactedAnnotation
        {
            Key = key,
            ValueLength = value.Length,
            ValueHashPrefix = hashPrefix,
            TruncatedValue = showValues ? Truncate(value, maxPreviewLength) : null
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 3), "...");
}
