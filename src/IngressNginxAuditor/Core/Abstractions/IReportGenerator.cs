using IngressNginxAuditor.Core.Models;

namespace IngressNginxAuditor.Core.Abstractions;

/// <summary>
/// Interface for generating scan reports in various formats.
/// </summary>
public interface IReportGenerator
{
    /// <summary>
    /// The format identifier for this generator (e.g., "md", "json").
    /// </summary>
    string Format { get; }

    /// <summary>
    /// The file extension for this format (e.g., ".md", ".json").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Generates a report from scan results.
    /// </summary>
    /// <param name="result">The scan result to generate a report from.</param>
    /// <param name="outputStream">The stream to write the report to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task GenerateAsync(
        ScanResult result,
        Stream outputStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a report and returns it as a string.
    /// </summary>
    /// <param name="result">The scan result to generate a report from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated report as a string.</returns>
    Task<string> GenerateToStringAsync(
        ScanResult result,
        CancellationToken cancellationToken = default);
}
