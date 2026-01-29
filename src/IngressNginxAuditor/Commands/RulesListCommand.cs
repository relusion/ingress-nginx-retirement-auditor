using System.ComponentModel;
using IngressNginxAuditor.Core.Rules;
using IngressNginxAuditor.Infrastructure;
using IngressNginxAuditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IngressNginxAuditor.Commands;

/// <summary>
/// Command settings for listing rules.
/// </summary>
public sealed class RulesListSettings : CommandSettings
{
    [CommandOption("--format")]
    [Description("Output format (table, json)")]
    [DefaultValue("table")]
    public string Format { get; init; } = "table";

    [CommandOption("--category")]
    [Description("Filter by category (detection, risk)")]
    public string? Category { get; init; }

    [CommandOption("--severity")]
    [Description("Filter by minimum severity")]
    public string? Severity { get; init; }
}

/// <summary>
/// Command to list all available detection rules.
/// </summary>
public sealed class RulesListCommand : Command<RulesListSettings>
{
    public override int Execute(CommandContext context, RulesListSettings settings)
    {
        try
        {
            // Build services
            var services = new ServiceCollection();
            services.AddAuditorServices();
            using var provider = services.BuildServiceProvider();

            var ruleEngine = provider.GetRequiredService<RuleEngine>();
            var rules = ruleEngine.GetAllRuleMetadata().ToList();

            // Apply filters
            if (!string.IsNullOrEmpty(settings.Category))
            {
                rules = rules.Where(r =>
                    r.Category.Equals(settings.Category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrEmpty(settings.Severity) &&
                Enum.TryParse<Core.Models.Enums.Severity>(settings.Severity, ignoreCase: true, out var minSeverity))
            {
                rules = rules.Where(r => r.DefaultSeverity >= minSeverity).ToList();
            }

            // Output
            if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                OutputJson(rules);
            }
            else
            {
                OutputTable(rules);
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.FatalError;
        }
    }

    private static void OutputTable(IReadOnlyList<RuleMetadata> rules)
    {
        if (rules.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No rules found matching the specified filters.[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Rule ID");
        table.AddColumn("Title");
        table.AddColumn("Category");
        table.AddColumn("Severity");
        table.AddColumn("Tags");

        foreach (var rule in rules.OrderBy(r => r.Category).ThenBy(r => r.RuleId))
        {
            var severityColor = GetSeverityColor(rule.DefaultSeverity);
            table.AddRow(
                $"[cyan]{rule.RuleId}[/]",
                rule.Title,
                rule.Category,
                $"[{severityColor}]{rule.DefaultSeverity}[/]",
                string.Join(", ", rule.Tags));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Total: {rules.Count} rules[/]");
    }

    private static void OutputJson(IReadOnlyList<RuleMetadata> rules)
    {
        var output = new
        {
            total = rules.Count,
            rules = rules.Select(r => new
            {
                id = r.RuleId,
                title = r.Title,
                category = r.Category,
                severity = r.DefaultSeverity.ToString().ToLowerInvariant(),
                tags = r.Tags.ToArray()
            }).ToArray()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(output, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        AnsiConsole.WriteLine(json);
    }

    private static string GetSeverityColor(Core.Models.Enums.Severity severity) => severity switch
    {
        Core.Models.Enums.Severity.Critical => "red",
        Core.Models.Enums.Severity.High => "orangered1",
        Core.Models.Enums.Severity.Medium => "yellow",
        Core.Models.Enums.Severity.Low => "blue",
        Core.Models.Enums.Severity.Info => "grey",
        _ => "white"
    };
}
