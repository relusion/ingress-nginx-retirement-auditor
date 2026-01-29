using System.Collections.Concurrent;
using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;
using IngressNginxAuditor.Core.Scoring;

namespace IngressNginxAuditor.Reporting;

/// <summary>
/// Aggregates findings for summary reporting.
/// Thread-safe for concurrent finding additions.
/// </summary>
public class FindingAggregator
{
    private readonly ConcurrentDictionary<string, NamespaceRollupBuilder> _byNamespace = new();
    private readonly int[] _bySeverity = new int[5]; // Info, Low, Medium, High, Critical
    private readonly RiskScorer _scorer = new();
    private int _totalResources;
    private int _totalIngresses;
    private int _nginxDependentIngresses;

    /// <summary>
    /// Adds a resource to the aggregator.
    /// </summary>
    public void AddResource(NormalizedResource resource)
    {
        Interlocked.Increment(ref _totalResources);

        if (resource.Kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _totalIngresses);
        }

        GetOrCreateNamespaceRollup(resource.Namespace).IncrementIngressCount(resource);
    }

    /// <summary>
    /// Adds a finding to the aggregator.
    /// </summary>
    public void AddFinding(Finding finding)
    {
        // Update severity counts
        Interlocked.Increment(ref _bySeverity[(int)finding.Severity]);

        // Update namespace rollup
        var rollup = GetOrCreateNamespaceRollup(finding.Resource.Namespace);
        rollup.AddFinding(finding);
    }

    /// <summary>
    /// Marks an Ingress as nginx-dependent.
    /// </summary>
    public void MarkNginxDependent(string ns)
    {
        Interlocked.Increment(ref _nginxDependentIngresses);
        GetOrCreateNamespaceRollup(ns).IncrementNginxDependent();
    }

    /// <summary>
    /// Gets the aggregated summary.
    /// </summary>
    public ScanSummary GetSummary(PolicyResult policy)
    {
        var severityCounts = new Dictionary<Severity, int>();
        for (int i = 0; i < _bySeverity.Length; i++)
        {
            severityCounts[(Severity)i] = _bySeverity[i];
        }

        var namespaceRollups = _byNamespace.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Build());

        return new ScanSummary
        {
            ResourcesScanned = _totalResources,
            IngressesScanned = _totalIngresses,
            NginxDependentIngresses = _nginxDependentIngresses,
            FindingsBySeverity = severityCounts,
            ByNamespace = namespaceRollups,
            Policy = policy
        };
    }

    private NamespaceRollupBuilder GetOrCreateNamespaceRollup(string ns)
    {
        return _byNamespace.GetOrAdd(ns, _ => new NamespaceRollupBuilder(ns, _scorer));
    }

    private class NamespaceRollupBuilder
    {
        private readonly string _namespace;
        private readonly RiskScorer _scorer;
        private readonly List<Finding> _findings = [];
        private readonly object _lock = new();
        private int _ingressCount;
        private int _nginxDependentCount;

        public NamespaceRollupBuilder(string ns, RiskScorer scorer)
        {
            _namespace = ns;
            _scorer = scorer;
        }

        public void IncrementIngressCount(NormalizedResource resource)
        {
            if (resource.Kind.Equals("Ingress", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _ingressCount);
            }
        }

        public void IncrementNginxDependent()
        {
            Interlocked.Increment(ref _nginxDependentCount);
        }

        public void AddFinding(Finding finding)
        {
            lock (_lock)
            {
                _findings.Add(finding);
            }
        }

        public NamespaceRollup Build()
        {
            List<Finding> findingsCopy;
            lock (_lock)
            {
                findingsCopy = [.. _findings];
            }

            var bySeverity = findingsCopy
                .GroupBy(f => f.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            // Ensure all severity levels are represented
            foreach (var severity in Enum.GetValues<Severity>())
            {
                bySeverity.TryAdd(severity, 0);
            }

            var maxRiskScore = findingsCopy.Count > 0
                ? _scorer.CalculateScore(findingsCopy)
                : 0;

            return new NamespaceRollup
            {
                Namespace = _namespace,
                IngressCount = _ingressCount,
                FindingCount = findingsCopy.Count,
                BySeverity = bySeverity,
                MaxRiskScore = maxRiskScore
            };
        }
    }
}
