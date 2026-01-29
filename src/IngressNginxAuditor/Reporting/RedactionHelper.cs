using IngressNginxAuditor.Core.Models;

namespace IngressNginxAuditor.Reporting;

/// <summary>
/// Helper for redacting sensitive annotation values per ADR-006.
/// </summary>
public static class RedactionHelper
{
    /// <summary>
    /// Creates a dictionary of redacted annotations from raw annotations.
    /// </summary>
    /// <param name="annotations">The raw annotation key-value pairs.</param>
    /// <param name="showValues">Whether to include truncated value previews.</param>
    /// <param name="maxPreviewLength">Maximum length for value previews.</param>
    /// <returns>Dictionary of redacted annotations.</returns>
    public static IReadOnlyDictionary<string, RedactedAnnotation> RedactAnnotations(
        IReadOnlyDictionary<string, string> annotations,
        bool showValues = false,
        int maxPreviewLength = 50)
    {
        return annotations.ToDictionary(
            kvp => kvp.Key,
            kvp => RedactedAnnotation.FromAnnotation(kvp.Key, kvp.Value, showValues, maxPreviewLength));
    }

    /// <summary>
    /// Redacts annotation values matching specified prefixes.
    /// </summary>
    public static IReadOnlyDictionary<string, RedactedAnnotation> RedactMatchingAnnotations(
        IReadOnlyDictionary<string, string> annotations,
        IEnumerable<string> prefixes,
        bool showValues = false)
    {
        return annotations
            .Where(kvp => prefixes.Any(p => kvp.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(
                kvp => kvp.Key,
                kvp => RedactedAnnotation.FromAnnotation(kvp.Key, kvp.Value, showValues));
    }
}
