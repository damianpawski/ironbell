namespace Ironbell.Domain.Training;

/// <summary>What the athlete is doing during a step.</summary>
public enum StepKind
{
    Work = 0,
    Rest = 1,
}

/// <summary>
/// One entry in the resolved session timeline: the unit the training screen advances through.
/// </summary>
/// <param name="Ordinal">Position in the session, from zero.</param>
/// <param name="Kind">Work or rest.</param>
/// <param name="Description">Coach-voice line for the screen, e.g. "10 swings @ 24 kg".</param>
/// <param name="Exercise">Movement, when the step has one. Rest steps do not.</param>
/// <param name="Reps">Target repetitions, when the step prescribes a count.</param>
/// <param name="Weight">Bell in use, when the step has one.</param>
/// <param name="Duration">
/// How long the step lasts, or <see langword="null"/> when only the athlete can say.
/// </param>
/// <remarks>
/// <para>
/// The nullable duration is the important part of this shape. An EMOM window or a rest period is
/// governed by the clock and its length is known before the session starts. A set of five heavy
/// presses is not: it takes as long as it takes, and the step after it cannot have a start time
/// until the set is logged.
/// </para>
/// <para>
/// Giving those steps an invented duration would let every screen pretend the whole session is on
/// a schedule. It would also quietly hollow out the ±250 ms accuracy gate, which is only a
/// meaningful claim where the clock genuinely rules. A null here says "ask the athlete", and that
/// honesty is the point.
/// </para>
/// </remarks>
public sealed record TimelineStep(
    int Ordinal,
    StepKind Kind,
    string Description,
    string? Exercise,
    int? Reps,
    BellWeight? Weight,
    TimeSpan? Duration)
{
    /// <summary>True when the clock owns this step; false when a logged set advances it.</summary>
    public bool IsTimed => Duration is not null;

    /// <summary>Weight moved by completing this step as prescribed.</summary>
    public decimal PrescribedTonnage =>
        Weight is { } weight && Reps is { } reps ? weight.TonnageFor(reps) : 0m;
}
