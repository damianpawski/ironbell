namespace Ironbell.Domain.Training;

/// <summary>
/// Expands a planned session into the ordered steps a training screen walks through.
/// </summary>
/// <remarks>
/// Pure and deterministic: the same session always resolves to the same steps. That is what lets a
/// committed golden file act as the specification for expansion.
/// </remarks>
public static class TimelineResolver
{
    public static ResolvedSession Resolve(TrainingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var blocks = new List<ResolvedBlock>(session.Blocks.Count);
        var ordinal = 0;

        foreach (var block in session.Blocks)
        {
            var (limit, steps) = Expand(block);

            // Ordinals run across the whole session, not per block: the athlete advances through
            // one list, and block boundaries are presentation.
            var numbered = new List<TimelineStep>(steps.Count);
            foreach (var step in steps)
            {
                numbered.Add(step with { Ordinal = ordinal });
                ordinal++;
            }

            blocks.Add(new ResolvedBlock(block.Name, limit, numbered));
        }

        return new ResolvedSession(session.Name, blocks);
    }

    private static (TimeSpan? Limit, IReadOnlyList<TimelineStep> Steps) Expand(TrainingBlock block) =>
        block switch
        {
            EmomBlock emom => ExpandEmom(emom),
            IntervalBlock interval => ExpandInterval(interval),
            AmrapBlock amrap => ExpandAmrap(amrap),
            ForTimeBlock forTime => ExpandForTime(forTime),
            _ => throw new NotSupportedException(
                $"'{block.GetType().Name}' is rep-driven and is not resolved yet."),
        };

    /// <summary>
    /// One step per window. The leftover time inside a window is the rest, so no rest step is
    /// emitted: an EMOM's contract is that the next round begins on the minute whether the work
    /// took twenty seconds or fifty.
    /// </summary>
    private static (TimeSpan?, IReadOnlyList<TimelineStep>) ExpandEmom(EmomBlock block)
    {
        var steps = new List<TimelineStep>(block.Rounds);

        for (var round = 0; round < block.Rounds; round++)
        {
            // The rotation cycles, so alternating work is one block rather than several.
            var effort = block.Rotation[round % block.Rotation.Count];
            steps.Add(Work(effort, block.Interval));
        }

        return (null, steps);
    }

    /// <summary>
    /// Work and rest alternating, with no rest after the final round — a countdown left running
    /// once the work is done is just a screen the athlete has to dismiss.
    /// </summary>
    private static (TimeSpan?, IReadOnlyList<TimelineStep>) ExpandInterval(IntervalBlock block)
    {
        var steps = new List<TimelineStep>(block.Rounds * 2);

        for (var round = 0; round < block.Rounds; round++)
        {
            steps.Add(new TimelineStep(
                Ordinal: 0,
                StepKind.Work,
                Description: $"{block.Exercise} @ {block.Weight} for {Describe(block.Work)}",
                Exercise: block.Exercise,
                // Reps are whatever the athlete manages inside the window, so none are prescribed.
                Reps: null,
                Weight: block.Weight,
                Duration: block.Work));

            if (round < block.Rounds - 1)
            {
                steps.Add(Rest(block.Rest));
            }
        }

        return (null, steps);
    }

    /// <summary>
    /// The steps describe one round; the athlete repeats them until the window closes. The window
    /// is the block's limit rather than any step's duration, because how long a round takes is
    /// exactly what an AMRAP is measuring.
    /// </summary>
    private static (TimeSpan?, IReadOnlyList<TimelineStep>) ExpandAmrap(AmrapBlock block)
    {
        var steps = new List<TimelineStep>(block.Round.Count);

        foreach (var effort in block.Round)
        {
            steps.Add(Work(effort, duration: null));
        }

        return (block.Window, steps);
    }

    /// <summary>
    /// Fixed work raced against the clock. Each task advances when logged; the cap, if any, bounds
    /// the block.
    /// </summary>
    private static (TimeSpan?, IReadOnlyList<TimelineStep>) ExpandForTime(ForTimeBlock block)
    {
        var steps = new List<TimelineStep>(block.Tasks.Count);

        foreach (var task in block.Tasks)
        {
            steps.Add(Work(task, duration: null));
        }

        return (block.Cap, steps);
    }

    private static TimelineStep Work(Effort effort, TimeSpan? duration) =>
        new(
            Ordinal: 0,
            StepKind.Work,
            Description: Describe(effort),
            effort.Exercise,
            effort.Reps,
            effort.Weight,
            duration);

    private static TimelineStep Rest(TimeSpan duration) =>
        new(
            Ordinal: 0,
            StepKind.Rest,
            Description: $"Rest {Describe(duration)}",
            Exercise: null,
            Reps: null,
            Weight: null,
            duration);

    private static string Describe(Effort effort) =>
        $"{effort.Reps} {effort.Exercise} @ {effort.Weight}";

    private static string Describe(TimeSpan duration) =>
        duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds:0.##} s"
            : $"{duration.TotalMinutes:0.##} min";
}
