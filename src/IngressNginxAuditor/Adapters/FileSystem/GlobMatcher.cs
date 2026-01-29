using IngressNginxAuditor.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace IngressNginxAuditor.Adapters.FileSystem;

/// <summary>
/// Matches files using glob patterns.
/// </summary>
public class GlobMatcher
{
    /// <summary>
    /// Finds all files matching the specified glob patterns.
    /// </summary>
    /// <param name="basePath">Base directory to search from.</param>
    /// <param name="includePatterns">Glob patterns to include (e.g., "**/*.yaml").</param>
    /// <param name="excludePatterns">Glob patterns to exclude (e.g., "**/node_modules/**").</param>
    /// <returns>List of matching file paths.</returns>
    public IReadOnlyList<string> MatchFiles(
        string basePath,
        IEnumerable<string>? includePatterns = null,
        IEnumerable<string>? excludePatterns = null)
    {
        if (!Directory.Exists(basePath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {basePath}");
        }

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);

        // Add include patterns
        var includes = includePatterns?.ToList() ?? DefaultConfiguration.YamlGlobPatterns.ToList();
        foreach (var pattern in includes)
        {
            matcher.AddInclude(pattern);
        }

        // Add exclude patterns
        var excludes = excludePatterns?.ToList() ?? DefaultConfiguration.ExcludePatterns.ToList();
        foreach (var pattern in excludes)
        {
            matcher.AddExclude(pattern);
        }

        var directoryInfo = new DirectoryInfo(basePath);
        var wrapper = new DirectoryInfoWrapper(directoryInfo);
        var result = matcher.Execute(wrapper);

        return result.Files
            .Select(f => Path.GetFullPath(Path.Combine(basePath, f.Path)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Checks if a single file matches the specified patterns.
    /// </summary>
    public bool IsMatch(
        string filePath,
        IEnumerable<string>? includePatterns = null,
        IEnumerable<string>? excludePatterns = null)
    {
        var fileName = Path.GetFileName(filePath);
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);

        var includes = includePatterns?.ToList() ?? DefaultConfiguration.YamlGlobPatterns.ToList();
        foreach (var pattern in includes)
        {
            // Simplify pattern for single file matching
            var simplePattern = pattern.Replace("**/", "").Replace("**", "*");
            matcher.AddInclude(simplePattern);
        }

        var excludes = excludePatterns?.ToList() ?? [];
        foreach (var pattern in excludes)
        {
            matcher.AddExclude(pattern);
        }

        var result = matcher.Match(fileName);
        return result.HasMatches;
    }

    /// <summary>
    /// Gets default YAML file patterns.
    /// </summary>
    public static IReadOnlyList<string> DefaultIncludePatterns => DefaultConfiguration.YamlGlobPatterns;

    /// <summary>
    /// Gets default exclude patterns.
    /// </summary>
    public static IReadOnlyList<string> DefaultExcludePatterns => DefaultConfiguration.ExcludePatterns;
}
