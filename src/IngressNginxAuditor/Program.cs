using IngressNginxAuditor.Commands;
using IngressNginxAuditor.Configuration;
using Spectre.Console.Cli;

namespace IngressNginxAuditor;

/// <summary>
/// Entry point for the ingress-nginx-retirement-auditor CLI tool.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();

        app.Configure(config =>
        {
            config.SetApplicationName("ingress-nginx-auditor");
            config.SetApplicationVersion(DefaultConfiguration.ToolVersion);

            // Scan commands
            config.AddBranch("scan", scan =>
            {
                scan.SetDescription("Scan for ingress-nginx usage");

                scan.AddCommand<ScanClusterCommand>("cluster")
                    .WithDescription("Scan a Kubernetes cluster for ingress-nginx usage")
                    .WithExample("scan", "cluster")
                    .WithExample("scan", "cluster", "--context", "prod-cluster")
                    .WithExample("scan", "cluster", "-n", "production,staging", "--fail-on", "high");

                scan.AddCommand<ScanRepoCommand>("repo")
                    .WithDescription("Scan YAML files in a repository for ingress-nginx usage")
                    .WithExample("scan", "repo", "--path", "./k8s")
                    .WithExample("scan", "repo", "--stdin")
                    .WithExample("scan", "repo", "-p", "./manifests", "-e", "**/test/**");
            });

            // Rules commands
            config.AddBranch("rules", rules =>
            {
                rules.SetDescription("Manage and inspect detection rules");

                rules.AddCommand<RulesListCommand>("list")
                    .WithDescription("List all available detection rules")
                    .WithExample("rules", "list")
                    .WithExample("rules", "list", "--category", "risk")
                    .WithExample("rules", "list", "--format", "json");

                rules.AddCommand<RulesExplainCommand>("explain")
                    .WithDescription("Show detailed information about a specific rule")
                    .WithExample("rules", "explain", "DET-NGINX-CLASS-001")
                    .WithExample("rules", "explain", "RISK-SNIPPET-001", "--format", "json");
            });
        });

        return await app.RunAsync(args);
    }
}
