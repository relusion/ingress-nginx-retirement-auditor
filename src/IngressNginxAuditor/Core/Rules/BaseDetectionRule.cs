using IngressNginxAuditor.Core.Models;
using IngressNginxAuditor.Core.Models.Enums;

namespace IngressNginxAuditor.Core.Rules;

/// <summary>
/// Base class for detection rules implementing the Template Method pattern.
/// Subclasses implement Matches and CollectEvidence methods.
/// </summary>
public abstract class BaseDetectionRule : IDetectionRule
{
    /// <inheritdoc />
    public abstract string RuleId { get; }

    /// <inheritdoc />
    public abstract string Title { get; }

    /// <inheritdoc />
    public abstract string Category { get; }

    /// <inheritdoc />
    public abstract Severity DefaultSeverity { get; }

    /// <inheritdoc />
    public abstract Confidence DefaultConfidence { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> Recommendations { get; }

    /// <summary>
    /// Gets the effective severity, which may be overridden.
    /// </summary>
    public Severity EffectiveSeverity { get; set; }

    /// <summary>
    /// Initializes the effective severity to the default.
    /// </summary>
    protected BaseDetectionRule()
    {
        // Note: This will be overwritten after construction since DefaultSeverity is abstract
        // The RuleRegistry will set EffectiveSeverity when applying overrides
    }

    /// <inheritdoc />
    public Finding? Evaluate(NormalizedResource resource, bool showAnnotationValues = false)
    {
        if (!Matches(resource))
            return null;

        var evidence = CollectEvidence(resource, showAnnotationValues);
        return CreateFinding(resource, evidence);
    }

    /// <inheritdoc />
    public RuleMetadata GetMetadata() => new()
    {
        RuleId = RuleId,
        Title = Title,
        Category = Category,
        DefaultSeverity = DefaultSeverity,
        DefaultConfidence = DefaultConfidence,
        Description = Description,
        Recommendations = Recommendations
    };

    /// <summary>
    /// Determines whether the rule matches the given resource.
    /// </summary>
    /// <param name="resource">The resource to check.</param>
    /// <returns>True if the rule matches, false otherwise.</returns>
    protected abstract bool Matches(NormalizedResource resource);

    /// <summary>
    /// Collects evidence from the resource that triggered the match.
    /// </summary>
    /// <param name="resource">The resource to collect evidence from.</param>
    /// <param name="showAnnotationValues">Whether to include annotation value previews.</param>
    /// <returns>Evidence for the finding.</returns>
    protected abstract Evidence CollectEvidence(NormalizedResource resource, bool showAnnotationValues);

    /// <summary>
    /// Creates a finding from the resource and evidence.
    /// </summary>
    protected Finding CreateFinding(NormalizedResource resource, Evidence evidence)
    {
        var severity = GetEffectiveSeverity(resource);

        return new Finding
        {
            Id = RuleId,
            Title = Title,
            Severity = severity,
            Confidence = DefaultConfidence,
            Category = Category,
            Resource = resource.ToResourceReference(),
            Evidence = evidence,
            Message = FormatMessage(resource),
            Recommendations = Recommendations
        };
    }

    /// <summary>
    /// Gets the effective severity for a given resource.
    /// Override this method to provide dynamic severity based on resource content.
    /// </summary>
    /// <param name="resource">The resource being evaluated.</param>
    /// <returns>The severity to use for this finding.</returns>
    protected virtual Severity GetEffectiveSeverity(NormalizedResource resource)
    {
        return EffectiveSeverity == default ? DefaultSeverity : EffectiveSeverity;
    }

    /// <summary>
    /// Formats the finding message. Override to customize.
    /// </summary>
    protected virtual string FormatMessage(NormalizedResource resource) =>
        $"{Title} in {resource.Kind} '{resource}'";
}
