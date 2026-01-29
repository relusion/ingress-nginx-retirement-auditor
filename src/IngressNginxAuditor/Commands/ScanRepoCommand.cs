using System.ComponentModel;
using IngressNginxAuditor.Adapters.FileSystem;
using IngressNginxAuditor.Configuration;
using IngressNginxAuditor.Core.Abstractions;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Infrastructure;
using IngressNginxAuditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IngressNginxAuditor.Commands;

/// <summary>
/// Command settings for repo scan.
/// </summary>
public sealed class ScanRepoSettings : CommandSettings
{
    [CommandOption("-p|--path")]
    [Description("Path to scan (directory containing YAML files)")]
    public string? Path { get; init; }

    [CommandOption("--stdin")]
    [Description("Read YAML from standard input")]
    public bool Stdin { get; init; }

    [CommandOption("-i|--include")]
    [Description("Glob patterns to include (comma-separated)")]
    [DefaultValue("**/*.yaml,**/*.yml")]
    public string IncludePatterns { get; init; } = "**/*.yaml,**/*.yml";

    [CommandOption("-e|--exclude")]
    [Description("Glob patterns to exclude (comma-separated)")]
    [DefaultValue("**/node_modules/**,**/.git/**")]
    public string ExcludePatterns { get; init; } = "**/node_modules/**,**/.git/**";

    [CommandOption("-f|--format")]
    [Description("Output formats (comma-separated: md,json)")]
    [DefaultValue("md,json")]
    public string Formats { get; init; } = "md,json";

    [CommandOption("-o|--output")]
    [Description("Output directory path")]
    public string? OutputPath { get; init; }

    [CommandOption("--fail-on")]
    [Description("Minimum severity to trigger failure (info, low, medium, high, critical)")]
    [DefaultValue("high")]
    public string FailOn { get; init; } = "high";

    [CommandOption("--show-annotation-values")]
    [Description("Show truncated annotation values in output")]
    public bool ShowAnnotationValues { get; init; }

    [CommandOption("--config")]
    [Description("Path to configuration file")]
    public string? ConfigPath { get; init; }

    public override ValidationResult Validate()
    {
        if (!Stdin && string.IsNullOrEmpty(Path))
        {
            return ValidationResult.Error("Either --path or --stdin must be specified");
        }

        if (Stdin && !string.IsNullOrEmpty(Path))
        {
            return ValidationResult.Error("Cannot use both --path and --stdin");
        }

        return ValidationResult.Success();
    }
}

/// <summary>
/// Command to scan YAML files in a repository for ingress-nginx usage.
/// </summary>
public sealed class ScanRepoCommand : AsyncCommand<ScanRepoSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ScanRepoSettings settings)
    {
        try
        {
            // Load configuration
            var config = LoadConfig(settings);

            // Build services
            var services = new ServiceCollection();
            services.AddAuditorServices();
            await using var provider = services.BuildServiceProvider();

            var orchestrator = provider.GetRequiredService<ScanOrchestrator>();

            // Create appropriate reader
            IResourceReader reader;
            var scannedPath = settings.Path ?? "<stdin>";

            if (settings.Stdin)
            {
                reader = new StdinResourceReader();
            }
            else
            {
                var basePath = settings.Path ?? Directory.GetCurrentDirectory();

                if (!Directory.Exists(basePath))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Directory not found: {basePath}");
                    return ExitCodes.InvalidConfig;
                }

                reader = new FileResourceReader();
            }

            // Build reader options
            var readerOptions = new ResourceReaderOptions
            {
                Path = settings.Path,
                GlobPatterns = ParseList(settings.IncludePatterns),
                ExcludePatterns = ParseList(settings.ExcludePatterns)
            };

            // Run scan
            var result = await AnsiConsole.Status()
                .StartAsync("Scanning files...", async ctx =>
                {
                    ctx.Status("Reading YAML files...");
                    return await orchestrator.ScanAsync(
                        reader,
                        readerOptions,
                        config,
                        ScanMode.Repo,
                        scannedPath: scannedPath);
                });

            // Check for empty stdin
            if (settings.Stdin && reader is StdinResourceReader stdinReader && !stdinReader.HadInput)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] No input received from stdin");
                return ExitCodes.PartialFailure;
            }

            // Display summary
            DisplaySummary(result);

            // Generate reports
            var formats = settings.Formats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            await orchestrator.GenerateReportsAsync(result, formats, settings.OutputPath);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Reports generated in: {settings.OutputPath ?? Directory.GetCurrentDirectory()}[/]");

            return result.Summary.Policy.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Fatal error:[/] {ex.Message}");
            return ExitCodes.FatalError;
        }
    }

    private static AuditorConfig LoadConfig(ScanRepoSettings settings)
    {
        AuditorConfig config;

        if (!string.IsNullOrEmpty(settings.ConfigPath))
        {
            var loader = new ConfigLoader();
            config = loader.LoadFromFile(settings.ConfigPath);
        }
        else
        {
            config = ConfigLoader.GetDefault();
        }

        // Apply command-line overrides
        if (Enum.TryParse<Severity>(settings.FailOn, ignoreCase: true, out var severity))
        {
            config = config with { Policy = config.Policy with { FailOn = severity } };
        }

        config = config with
        {
            Output = config.Output with { ShowAnnotationValues = settings.ShowAnnotationValues }
        };

        return config;
    }

    private static IReadOnlyList<string>? ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static void DisplaySummary(Core.Models.ScanResult result)
    {
        AnsiConsole.WriteLine();

        // Policy status
        var statusColor = result.Summary.Policy.Passed ? "green" : "red";
        var statusText = result.Summary.Policy.Passed ? "PASS" : "FAIL";
        AnsiConsole.MarkupLine($"[bold {statusColor}]Policy Status: {statusText}[/]");
        AnsiConsole.WriteLine();

        // Summary table
        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");

        table.AddRow("Resources Scanned", result.Summary.ResourcesScanned.ToString());
        table.AddRow("Ingresses Scanned", result.Summary.IngressesScanned.ToString());
        table.AddRow("NGINX-Dependent", result.Summary.NginxDependentIngresses.ToString());
        table.AddRow("Total Findings", result.Summary.TotalFindings.ToString());

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Severity breakdown
        if (result.Summary.TotalFindings > 0)
        {
            var severityTable = new Table();
            severityTable.AddColumn("Severity");
            severityTable.AddColumn("Count");

            foreach (var (sev, count) in result.Summary.FindingsBySeverity.OrderByDescending(kvp => kvp.Key))
            {
                if (count > 0)
                {
                    var color = GetSeverityColor(sev);
                    severityTable.AddRow($"[{color}]{sev}[/]", count.ToString());
                }
            }

            AnsiConsole.Write(severityTable);
        }

        // Warnings
        if (result.Warnings.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Warnings:[/]");
            foreach (var warning in result.Warnings)
            {
                AnsiConsole.MarkupLine($"  [yellow]•[/] {warning}");
            }
        }

        // Errors
        if (result.Errors.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Errors:[/]");
            foreach (var error in result.Errors)
            {
                AnsiConsole.MarkupLine($"  [red]•[/] {error}");
            }
        }
    }

    private static string GetSeverityColor(Severity severity) => severity switch
    {
        Severity.Critical => "red",
        Severity.High => "orangered1",
        Severity.Medium => "yellow",
        Severity.Low => "blue",
        Severity.Info => "grey",
        _ => "white"
    };
}
