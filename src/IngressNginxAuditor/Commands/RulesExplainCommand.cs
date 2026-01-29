using System.ComponentModel;
using IngressNginxAuditor.Core.Rules;
using IngressNginxAuditor.Infrastructure;
using IngressNginxAuditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IngressNginxAuditor.Commands;

/// <summary>
/// Command settings for explaining a rule.
/// </summary>
public sealed class RulesExplainSettings : CommandSettings
{
    [CommandArgument(0, "<RULE_ID>")]
    [Description("The rule ID to explain")]
    public string RuleId { get; init; } = string.Empty;

    [CommandOption("--format")]
    [Description("Output format (text, json)")]
    [DefaultValue("text")]
    public string Format { get; init; } = "text";
}

/// <summary>
/// Command to show detailed information about a specific rule.
/// </summary>
public sealed class RulesExplainCommand : Command<RulesExplainSettings>
{
    public override int Execute(CommandContext context, RulesExplainSettings settings)
    {
        try
        {
            // Build services
            var services = new ServiceCollection();
            services.AddAuditorServices();
            using var provider = services.BuildServiceProvider();

            var ruleEngine = provider.GetRequiredService<RuleEngine>();
            var metadata = ruleEngine.GetRuleMetadata(settings.RuleId);

            if (metadata == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Rule '{settings.RuleId}' not found.");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Use 'rules list' to see all available rules.[/]");
                return ExitCodes.InvalidConfig;
            }

            if (settings.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                OutputJson(metadata);
            }
            else
            {
                OutputText(metadata);
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.FatalError;
        }
    }

    private static void OutputText(RuleMetadata metadata)
    {
        var severityColor = GetSeverityColor(metadata.DefaultSeverity);

        // Header
        var panel = new Panel($"[bold]{metadata.Title}[/]")
        {
            Header = new PanelHeader($"[cyan]{metadata.RuleId}[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Details
        var table = new Table { Border = TableBorder.None, ShowHeaders = false };
        table.AddColumn("Key");
        table.AddColumn("Value");

        table.AddRow("[bold]Category:[/]", metadata.Category);
        table.AddRow("[bold]Severity:[/]", $"[{severityColor}]{metadata.DefaultSeverity}[/]");
        table.AddRow("[bold]Confidence:[/]", metadata.DefaultConfidence.ToString());
        table.AddRow("[bold]Tags:[/]", string.Join(", ", metadata.Tags));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Description
        AnsiConsole.MarkupLine("[bold]Description:[/]");
        AnsiConsole.WriteLine(metadata.Description);
        AnsiConsole.WriteLine();

        // Rationale
        if (!string.IsNullOrEmpty(metadata.Rationale))
        {
            AnsiConsole.MarkupLine("[bold]Rationale:[/]");
            AnsiConsole.WriteLine(metadata.Rationale);
            AnsiConsole.WriteLine();
        }

        // Recommendations
        if (metadata.Recommendations.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Recommendations:[/]");
            foreach (var rec in metadata.Recommendations)
            {
                AnsiConsole.MarkupLine($"  [green]•[/] {rec}");
            }
            AnsiConsole.WriteLine();
        }

        // References
        if (metadata.References.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]References:[/]");
            foreach (var reference in metadata.References)
            {
                AnsiConsole.MarkupLine($"  [blue]•[/] [link]{reference}[/]");
            }
        }
    }

    private static void OutputJson(RuleMetadata metadata)
    {
        var output = new
        {
            id = metadata.RuleId,
            title = metadata.Title,
            category = metadata.Category,
            description = metadata.Description,
            rationale = metadata.Rationale,
            severity = metadata.DefaultSeverity.ToString().ToLowerInvariant(),
            confidence = metadata.DefaultConfidence.ToString().ToLowerInvariant(),
            tags = metadata.Tags.ToArray(),
            recommendations = metadata.Recommendations.ToArray(),
            references = metadata.References.ToArray()
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
