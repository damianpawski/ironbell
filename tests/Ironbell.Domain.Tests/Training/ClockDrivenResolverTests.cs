using Ironbell.Domain.Training;

namespace Ironbell.Domain.Tests.Training;

/// <summary>
/// The four blocks whose shape the clock decides. Their offsets are knowable before the session
/// starts, which is why they are resolved first: they pin down the output before the rep-driven
/// blocks get a say.
/// </summary>
public class ClockDrivenResolverTests
{
    private static readonly BellWeight Bell24 = new(24);
    private static readonly BellWeight Bell32 = new(32);

    private static ResolvedBlock ResolveSingle(TrainingBlock block) =>
        TimelineResolver.Resolve(new TrainingSession("Test", [block])).Blocks.Single();

    // --- EMOM ----------------------------------------------------------------------------------

    [Fact]
    public void Emom_emits_one_timed_step_per_round()
    {
        var block = new EmomBlock("Swings", Rounds: 10, TimeSpan.FromMinutes(1),
            [new Effort("Swing", 10, Bell24)]);

        var resolved = ResolveSingle(block);

        resolved.Steps.Count.ShouldBe(10);
        resolved.Steps.ShouldAllBe(step => step.Kind == StepKind.Work);
        resolved.Steps.ShouldAllBe(step => step.Duration == TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Emom_emits_no_rest_steps()
    {
        var block = new EmomBlock("Swings", Rounds: 5, TimeSpan.FromMinutes(1),
            [new Effort("Swing", 10, Bell24)]);

        var resolved = ResolveSingle(block);

        // Leftover time inside a window is the rest. The contract is that the next round starts on
        // the minute whether the work took twenty seconds or fifty, so a rest step would be a lie
        // about a duration nobody controls.
        resolved.Steps.ShouldNotContain(step => step.Kind == StepKind.Rest);
    }

    [Fact]
    public void Emom_cycles_the_rotation_so_alternating_work_is_one_block()
    {
        var block = new EmomBlock("Alternating", Rounds: 4, TimeSpan.FromMinutes(1),
        [
            new Effort("Swing", 10, Bell24),
            new Effort("Snatch", 8, Bell24),
        ]);

        var resolved = ResolveSingle(block);

        resolved.Steps.Select(step => step.Exercise)
            .ShouldBe(["Swing", "Snatch", "Swing", "Snatch"]);
    }

    [Fact]
    public void Emom_lasts_rounds_times_the_interval()
    {
        var block = new EmomBlock("Swings", Rounds: 10, TimeSpan.FromMinutes(1),
            [new Effort("Swing", 10, Bell24)]);

        var resolved = ResolveSingle(block);

        resolved.Steps.Sum(step => step.Duration!.Value.TotalMinutes).ShouldBe(10);
    }

    // --- Interval ------------------------------------------------------------------------------

    [Fact]
    public void Interval_alternates_work_and_rest()
    {
        var block = new IntervalBlock("On the minute", "Snatch", Bell24,
            Work: TimeSpan.FromSeconds(30), Rest: TimeSpan.FromSeconds(30), Rounds: 3);

        var resolved = ResolveSingle(block);

        resolved.Steps.Select(step => step.Kind)
            .ShouldBe([StepKind.Work, StepKind.Rest, StepKind.Work, StepKind.Rest, StepKind.Work]);
    }

    [Fact]
    public void Interval_does_not_rest_after_the_final_round()
    {
        var block = new IntervalBlock("On the minute", "Snatch", Bell24,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), Rounds: 3);

        var resolved = ResolveSingle(block);

        // A countdown still running after the last set is a screen the athlete has to dismiss.
        resolved.Steps[^1].Kind.ShouldBe(StepKind.Work);
    }

    [Fact]
    public void Interval_work_prescribes_no_reps()
    {
        var block = new IntervalBlock("On the minute", "Snatch", Bell24,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), Rounds: 2);

        var resolved = ResolveSingle(block);

        // Reps are whatever fits in the window, so prescribing a count would be inventing one.
        resolved.Steps.Where(step => step.Kind == StepKind.Work)
            .ShouldAllBe(step => step.Reps == null);
    }

    // --- AMRAP ---------------------------------------------------------------------------------

    [Fact]
    public void Amrap_puts_the_window_on_the_block_not_the_steps()
    {
        var block = new AmrapBlock("Twelve minutes", TimeSpan.FromMinutes(12),
        [
            new Effort("Swing", 10, Bell24),
            new Effort("Goblet squat", 5, Bell24),
        ]);

        var resolved = ResolveSingle(block);

        resolved.Limit.ShouldBe(TimeSpan.FromMinutes(12));
        // How long a round takes is precisely what an AMRAP measures, so no step may claim one.
        resolved.Steps.ShouldAllBe(step => step.Duration == null);
    }

    [Fact]
    public void Amrap_steps_describe_a_single_round()
    {
        var block = new AmrapBlock("Twelve minutes", TimeSpan.FromMinutes(12),
        [
            new Effort("Swing", 10, Bell24),
            new Effort("Goblet squat", 5, Bell24),
        ]);

        var resolved = ResolveSingle(block);

        resolved.Steps.Count.ShouldBe(2);
    }

    [Fact]
    public void Amrap_prescribes_no_tonnage_because_rounds_are_unknown()
    {
        var session = new TrainingSession("Test",
        [
            new AmrapBlock("Twelve minutes", TimeSpan.FromMinutes(12),
                [new Effort("Swing", 10, Bell24)]),
        ]);

        var resolved = TimelineResolver.Resolve(session);

        // 240 kg per round, but nobody knows how many rounds. Reporting one round's worth would
        // understate every AMRAP ever trained; the honest prescribed figure is what is planned.
        resolved.PrescribedTonnage.ShouldBe(240m);
    }

    // --- ForTime -------------------------------------------------------------------------------

    [Fact]
    public void ForTime_tasks_advance_on_log_and_the_cap_bounds_the_block()
    {
        var block = new ForTimeBlock("100 snatches",
            [new Effort("Snatch", 100, Bell24)],
            Cap: TimeSpan.FromMinutes(10));

        var resolved = ResolveSingle(block);

        resolved.Limit.ShouldBe(TimeSpan.FromMinutes(10));
        resolved.Steps.ShouldAllBe(step => step.Duration == null);
    }

    [Fact]
    public void ForTime_without_a_cap_has_no_limit()
    {
        var block = new ForTimeBlock("100 snatches",
            [new Effort("Snatch", 100, Bell24)], Cap: null);

        var resolved = ResolveSingle(block);

        resolved.Limit.ShouldBeNull();
    }

    // --- session-level -------------------------------------------------------------------------

    [Fact]
    public void Ordinals_run_across_the_whole_session_not_per_block()
    {
        var session = new TrainingSession("Two blocks",
        [
            new EmomBlock("A", Rounds: 3, TimeSpan.FromMinutes(1), [new Effort("Swing", 10, Bell24)]),
            new ForTimeBlock("B", [new Effort("Snatch", 50, Bell32)], Cap: null),
        ]);

        var resolved = TimelineResolver.Resolve(session);

        // The athlete advances through one list; block boundaries are presentation.
        resolved.Steps.Select(step => step.Ordinal).ShouldBe([0, 1, 2, 3]);
    }

    [Fact]
    public void A_rep_driven_block_is_refused_rather_than_silently_dropped()
    {
        var session = new TrainingSession("Not yet",
        [
            new StraightBlock("Press", "Press", Sets: 5, Reps: 5, Bell24, TimeSpan.FromSeconds(90)),
        ]);

        // Returning an empty step list would look like a session with nothing in it.
        Should.Throw<NotSupportedException>(() => TimelineResolver.Resolve(session));
    }
}
