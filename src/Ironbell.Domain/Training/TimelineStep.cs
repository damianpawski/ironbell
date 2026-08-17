namespace Ironbell.Domain.Training;

/// <summary>What the athlete is doing during a step.</summary>
public enum StepKind
{
    Work = 0,
    Rest = 1,
}

/// <summary>
/// One entry in the resolved session timeline.
/// </summary>
/// <param name="Ordinal">Position in the session, from zero.</param>
/// <param name="Kind">Work or rest.</param>
/// <param name="Description">Coach-voice line for the screen, e.g. "10 × Swing @ 24 kg".</param>
/// <param name="Efforts">
/// The movements this step covers. Usually one; empty for rest.
/// </param>
/// <param name="Duration">
/// How long the step lasts, or <see langword="null"/> when only the athlete can say.
/// </param>
/// <remarks>
/// <para>
/// The nullable duration is the important part of this shape. An EMOM window or a rest period is
/// governed by the clock and its length is known before the session starts. A set of five heavy
/// presses is not: it takes as long as it takes, and the step after it cannot have a start time
/// until the set is logged. Giving those steps an invented duration would let every screen pretend
/// the session runs to a schedule, and would hollow out the ±250 ms accuracy gate, which is only a
/// meaningful claim where the clock genuinely rules.
/// </para>
/// <para>
/// A step holds a <em>list</em> of efforts rather than one, because a complex or a chain is a
/// single unbroken effort across several movements — the bell never touches down, so there is no
/// point inside it at which a set could be logged. Modelling those as one effort would have made
/// their prescribed tonnage zero, and tonnage is the headline metric of the app.
/// </para>
/// </remarks>
public sealed record TimelineStep(
    int Ordinal,
    StepKind Kind,
    string Description,
    IReadOnlyList<Effort> Efforts,
    TimeSpan? Duration)
{
    /// <summary>True when the clock owns this step; false when a logged set advances it.</summary>
    public bool IsTimed => Duration is not null;

    /// <summary>Weight moved by completing this step as prescribed.</summary>
    public decimal PrescribedTonnage => Efforts.Sum(effort => effort.PrescribedTonnage);
}
