using System.ComponentModel;
using IngressNginxAuditor.Adapters.Kubernetes;
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
/// Command settings for cluster scan.
/// </summary>
public sealed class ScanClusterSettings : CommandSettings
{
    [CommandOption("-k|--kubeconfig")]
    [Description("Path to kubeconfig file")]
    public string? KubeconfigPath { get; init; }

    [CommandOption("-c|--context")]
    [Description("Kubernetes context to use")]
    public string? Context { get; init; }

    [CommandOption("-n|--namespace")]
    [Description("Namespaces to scan (comma-separated)")]
    public string? Namespaces { get; init; }

    [CommandOption("--exclude-namespace")]
    [Description("Namespaces to exclude (comma-separated)")]
    public string? ExcludeNamespaces { get; init; }

    [CommandOption("-l|--label-selector")]
    [Description("Kubernetes label selector")]
    public string? LabelSelector { get; init; }

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

    [CommandOption("--api-concurrency")]
    [Description("Maximum concurrent Kubernetes API calls")]
    [DefaultValue(10)]
    public int ApiConcurrency { get; init; } = 10;

    [CommandOption("--timeout")]
    [Description("Scan timeout in seconds")]
    [DefaultValue(30)]
    public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Command to scan a Kubernetes cluster for ingress-nginx usage.
/// </summary>
public sealed class ScanClusterCommand : AsyncCommand<ScanClusterSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ScanClusterSettings settings)
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

            // Create Kubernetes client
            var authenticator = new KubernetesAuthenticator();
            k8s.IKubernetes k8sClient;

            try
            {
                k8sClient = authenticator.CreateClient(settings.KubeconfigPath, settings.Context);
            }
            catch (FileNotFoundException ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                return ExitCodes.FatalError;
            }
            catch (ArgumentException ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                return ExitCodes.InvalidConfig;
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                return ExitCodes.FatalError;
            }

            // Create reader
            using var boundedClient = new BoundedKubernetesClient(k8sClient, settings.ApiConcurrency);
            using var reader = new ClusterResourceReader(boundedClient);

            // Build reader options
            var readerOptions = new ResourceReaderOptions
            {
                IncludeNamespaces = ParseSet(settings.Namespaces),
                ExcludeNamespaces = ParseSet(settings.ExcludeNamespaces),
                LabelSelector = settings.LabelSelector,
                MaxConcurrency = settings.ApiConcurrency,
                Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
            };

            // Run scan with progress
            var result = await AnsiConsole.Status()
                .StartAsync("Scanning cluster...", async ctx =>
                {
                    ctx.Status("Connecting to cluster...");
                    await Task.Delay(100); // Brief pause for visual feedback

                    ctx.Status("Scanning resources...");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                    return await orchestrator.ScanAsync(
                        reader,
                        readerOptions,
                        config,
                        ScanMode.Cluster,
                        settings.Context,
                        cancellationToken: cts.Token);
                });

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

    private static AuditorConfig LoadConfig(ScanClusterSettings settings)
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
            Output = config.Output with { ShowAnnotationValues = settings.ShowAnnotationValues },
            Cluster = config.Cluster with
            {
                ApiConcurrency = settings.ApiConcurrency,
                TimeoutSeconds = settings.TimeoutSeconds
            }
        };

        return config;
    }

    private static IReadOnlySet<string>? ParseSet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
