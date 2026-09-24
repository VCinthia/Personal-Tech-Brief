namespace PersonalTechBrief.Domain.Briefs;

/// <summary>Lifecycle of a <see cref="Brief"/> (data model §11).</summary>
public enum BriefStatus
{
    /// <summary>The brief was created and its items are being generated asynchronously.</summary>
    Generating,

    /// <summary>Generation finished; the brief is an immutable snapshot (possibly with zero items).</summary>
    Completed,

    /// <summary>Generation could not complete; the brief carries no items.</summary>
    Failed,
}
