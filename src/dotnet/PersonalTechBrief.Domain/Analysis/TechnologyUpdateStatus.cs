namespace PersonalTechBrief.Domain.Analysis;

/// <summary>
/// Lifecycle state of a <see cref="TechnologyUpdate"/>. Grouping in this slice only produces
/// active updates; later slices may introduce additional terminal states.
/// </summary>
public enum TechnologyUpdateStatus
{
    Active,
}
