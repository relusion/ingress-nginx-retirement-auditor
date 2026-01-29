namespace IngressNginxAuditor.Core.Models.Enums;

/// <summary>
/// Severity level for findings, ordered from lowest to highest.
/// Used for risk scoring and policy evaluation.
/// </summary>
public enum Severity
{
    /// <summary>Informational finding, no migration action required.</summary>
    Info = 0,

    /// <summary>Low severity, minor migration consideration.</summary>
    Low = 1,

    /// <summary>Medium severity, moderate migration effort expected.</summary>
    Medium = 2,

    /// <summary>High severity, significant migration effort required.</summary>
    High = 3,

    /// <summary>Critical severity, blocking issue requiring immediate attention.</summary>
    Critical = 4
}
