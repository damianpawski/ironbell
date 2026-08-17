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
            // Clock-driven: offsets are knowable before the session starts.
            EmomBlock emom => ExpandEmom(emom),
            IntervalBlock interval => ExpandInterval(interval),
            AmrapBlock amrap => ExpandAmrap(amrap),
            ForTimeBlock forTime => ExpandForTime(forTime),

            // Rep-driven: the athlete's pace decides how long the work takes.
            StraightBlock straight => ExpandStraight(straight),
            CircuitBlock circuit => ExpandCircuit(circuit),
            LadderBlock ladder => ExpandLadder(ladder),
            ComplexBlock complex => ExpandComplex(complex),
            ChainBlock chain => ExpandChain(chain),

            _ => throw new NotSupportedException(
                $"No expansion is defined for '{block.GetType().Name}'."),
        };

    // --- clock-driven ------------------------------------------------------------------------

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
            steps.Add(Work([effort], block.Interval));
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

        // Reps are whatever fits the window, so none are prescribed.
        var effort = new Effort(block.Exercise, Reps: null, block.Weight, block.Bells);

        for (var round = 0; round < block.Rounds; round++)
        {
            steps.Add(new TimelineStep(
                Ordinal: 0,
                StepKind.Work,
                $"{block.Exercise} @ {block.Weight} for {Describe(block.Work)}",
                [effort],
                block.Work));

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
            steps.Add(Work([effort], duration: null));
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
            steps.Add(Work([task], duration: null));
        }

        return (block.Cap, steps);
    }

    // --- rep-driven --------------------------------------------------------------------------

    /// <summary>Sets of a single movement, resting between them but not after the last.</summary>
    private static (TimeSpan?, IReadOnlyList<TimelineStep>) ExpandStraight(StraightBlock block)
    {
        var steps = new List<TimelineStep>(block.Sets * 2);
        var effort = new Effort(block.Exercise, block.Reps, block.Weight, block.Bells);

        for (var set = 0; set < block.Sets; set++)
        {
            steps.Add(Work([effort], duration: null));

            if (set < block.Sets - 1)
            {
                steps.Add(Rest(block.Rest));
            }
        }

        return (null, steps);
    }

    /// <summary>
    /// Stations in order, repeated for rounds. No rest between stations — moving straight to the
    /// next station is what makes it a circuit.
    /// </summary>
    private static (TimeSpan?, IReadOnlyList<TimelineStep>) ExpandCircuit(CircuitBlock block)
    {
        var steps = new List<TimelineStep>((block.Stations.Count + 1) * block.Rounds);

        for (var round = 0; round < block.Rounds; round++)
        {
            foreach (var station in block.Stations)
            {
                steps.Add(Work([station], duration: null));
            }

            if (round < block.Rounds - 1)
            {
                steps.Add(Rest(block.RestBetweenRounds));
            }
        }

        return (null, steps);
    }

    /// <summary>
    /// Each rung is its own set, because the rep count changes and each is logged separately.
    /// Rest falls between rounds, not between rungs — climbing the ladder is the unbroken part.
    /// </summary>
    private static (TimeSpan?, IReadOnlyList<TimelineStep>) ExpandLadder(LadderBlock block)
    {
        var steps = new List<TimelineStep>((block.Rungs.Count + 1) * block.Rounds);

        for (var round = 0; round < block.Rounds; round++)
        {
            foreach (var rung in block.Rungs)
            {
                steps.Add(Work([new Effort(block.Exercise, rung, block.Weight, block.Bells)], duration: null));
            }

            if (round < block.Rounds - 1)
            {
                steps.Add(Rest(block.RestBetweenRounds));
            }
        }

        return (null, steps);
    }

    /// <summary>
    /// One step per set covering every movement. The bell is never set down inside a complex, so
    /// there is no point within it at which a set could be logged — the whole thing is one effort.
    /// </summary>
    private static (TimeSpan?, IReadOnlyList<TimelineStep>) ExpandComplex(ComplexBlock block)
    {
        var steps = new List<TimelineStep>(block.Sets * 2);
        var description = string.Join(" + ", block.Movements.Select(Describe));

        for (var set = 0; set < block.Sets; set++)
        {
            steps.Add(new TimelineStep(0, StepKind.Work, description, block.Movements, null));

            if (set < block.Sets - 1)
            {
                steps.Add(Rest(block.Rest));
            }
        }

        return (null, steps);
    }

    /// <summary>
    /// One step per set, like a complex, but the reps are the per-cycle reps multiplied by the
    /// number of cycles. The totals match an equivalent complex; the description carries the
    /// difference, because performing clean-press five times is not five cleans then five presses.
    /// </summary>
    private static (TimeSpan?, IReadOnlyList<TimelineStep>) ExpandChain(ChainBlock block)
    {
        var steps = new List<TimelineStep>(block.Sets * 2);

        var perSet = block.Links
            .Select(link => link with { Reps = link.Reps * block.Cycles })
            .ToList();

        var cycle = string.Join(" + ", block.Links.Select(Describe));
        var description = $"{block.Cycles} × ({cycle})";

        for (var set = 0; set < block.Sets; set++)
        {
            steps.Add(new TimelineStep(0, StepKind.Work, description, perSet, null));

            if (set < block.Sets - 1)
            {
                steps.Add(Rest(block.Rest));
            }
        }

        return (null, steps);
    }

    // --- helpers -----------------------------------------------------------------------------

    private static TimelineStep Work(IReadOnlyList<Effort> efforts, TimeSpan? duration) =>
        new(0, StepKind.Work, string.Join(" + ", efforts.Select(Describe)), efforts, duration);

    private static TimelineStep Rest(TimeSpan duration) =>
        new(0, StepKind.Rest, $"Rest {Describe(duration)}", [], duration);

    private static string Describe(Effort effort) =>
        effort.Reps is { } reps
            ? $"{reps} × {effort.Exercise} @ {effort.Weight}"
            : $"{effort.Exercise} @ {effort.Weight}";

    private static string Describe(TimeSpan duration) =>
        duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds:0.##} s"
            : $"{duration.TotalMinutes:0.##} min";
}
