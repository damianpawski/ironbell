using Ironbell.Domain.Training;

namespace Ironbell.Domain.Tests.Training;

/// <summary>
/// The five blocks whose length the athlete decides. None of their work steps carries a duration —
/// that is the point — so the assertions are about ordering, rest placement and tonnage.
/// </summary>
public class RepDrivenResolverTests
{
    private static readonly BellWeight Bell24 = new(24);
    private static readonly TimeSpan NinetySeconds = TimeSpan.FromSeconds(90);

    private static ResolvedBlock ResolveSingle(TrainingBlock block) =>
        TimelineResolver.Resolve(new TrainingSession("Test", [block])).Blocks.Single();

    [Fact]
    public void No_rep_driven_work_step_claims_a_duration()
    {
        TrainingBlock[] blocks =
        [
            new StraightBlock("A", "Press", 3, 5, Bell24, NinetySeconds),
            new CircuitBlock("B", [new Effort("Swing", 10, Bell24)], 2, NinetySeconds),
            new LadderBlock("C", "Press", Bell24, [1, 2, 3], 2, NinetySeconds),
            new ComplexBlock("D", [new Effort("Clean", 5, Bell24)], 2, NinetySeconds),
            new ChainBlock("E", [new Effort("Clean", 1, Bell24)], 5, 2, NinetySeconds),
        ];

        foreach (var block in blocks)
        {
            ResolveSingle(block).Steps
                .Where(step => step.Kind == StepKind.Work)
                .ShouldAllBe(step => step.Duration == null);
        }
    }

    // --- Straight ------------------------------------------------------------------------------

    [Fact]
    public void Straight_alternates_sets_and_rest_without_a_trailing_rest()
    {
        var resolved = ResolveSingle(
            new StraightBlock("Press", "Press", Sets: 3, Reps: 5, Bell24, NinetySeconds));

        resolved.Steps.Select(step => step.Kind)
            .ShouldBe([StepKind.Work, StepKind.Rest, StepKind.Work, StepKind.Rest, StepKind.Work]);
    }

    [Fact]
    public void Straight_tonnage_is_sets_times_reps_times_weight()
    {
        var resolved = TimelineResolver.Resolve(new TrainingSession("Test",
            [new StraightBlock("Press", "Press", 5, 5, Bell24, NinetySeconds)]));

        // 5 × 5 × 24
        resolved.PrescribedTonnage.ShouldBe(600m);
    }

    // --- Circuit -------------------------------------------------------------------------------

    [Fact]
    public void Circuit_runs_stations_back_to_back_and_rests_only_between_rounds()
    {
        var resolved = ResolveSingle(new CircuitBlock("Circuit",
        [
            new Effort("Swing", 10, Bell24),
            new Effort("Goblet squat", 5, Bell24),
        ], Rounds: 2, NinetySeconds));

        // Moving straight to the next station is what makes it a circuit; a rest between stations
        // would quietly turn it into two straight blocks.
        resolved.Steps.Select(step => step.Kind).ShouldBe(
            [StepKind.Work, StepKind.Work, StepKind.Rest, StepKind.Work, StepKind.Work]);
    }

    // --- Ladder --------------------------------------------------------------------------------

    [Fact]
    public void Ladder_emits_a_set_per_rung_and_rests_between_rounds()
    {
        var resolved = ResolveSingle(
            new LadderBlock("Ladder", "Press", Bell24, Rungs: [1, 2, 3], Rounds: 2, NinetySeconds));

        resolved.Steps.Where(step => step.Kind == StepKind.Work)
            .Select(step => step.Efforts.Single().Reps)
            .ShouldBe([1, 2, 3, 1, 2, 3]);

        resolved.Steps.Count(step => step.Kind == StepKind.Rest).ShouldBe(1);
    }

    [Fact]
    public void Ladder_tonnage_counts_every_rung()
    {
        var resolved = TimelineResolver.Resolve(new TrainingSession("Test",
            [new LadderBlock("Ladder", "Press", Bell24, [1, 2, 3], Rounds: 5, NinetySeconds)]));

        // (1+2+3) × 5 rounds × 24 kg
        resolved.PrescribedTonnage.ShouldBe(720m);
    }

    // --- Complex vs Chain ----------------------------------------------------------------------

    [Fact]
    public void Complex_is_one_step_per_set_covering_every_movement()
    {
        var resolved = ResolveSingle(new ComplexBlock("Complex",
        [
            new Effort("Clean", 5, Bell24),
            new Effort("Press", 5, Bell24),
            new Effort("Squat", 5, Bell24),
        ], Sets: 3, NinetySeconds));

        // The bell is never set down inside a complex, so there is no point within it at which a
        // set could be logged.
        var work = resolved.Steps.Where(step => step.Kind == StepKind.Work).ToList();
        work.Count.ShouldBe(3);
        work.ShouldAllBe(step => step.Efforts.Count == 3);
    }

    [Fact]
    public void Chain_multiplies_reps_by_cycles()
    {
        var resolved = ResolveSingle(new ChainBlock("Chain",
        [
            new Effort("Clean", 1, Bell24),
            new Effort("Press", 1, Bell24),
        ], Cycles: 5, Sets: 3, NinetySeconds));

        var firstSet = resolved.Steps.First(step => step.Kind == StepKind.Work);

        // One clean and one press, five times through: five of each per set.
        firstSet.Efforts.Select(effort => effort.Reps).ShouldBe([5, 5]);
    }

    [Fact]
    public void A_chain_and_the_equivalent_complex_move_the_same_weight()
    {
        var complex = TimelineResolver.Resolve(new TrainingSession("Complex",
        [
            new ComplexBlock("C", [new Effort("Clean", 5, Bell24), new Effort("Press", 5, Bell24)],
                Sets: 3, NinetySeconds),
        ]));

        var chain = TimelineResolver.Resolve(new TrainingSession("Chain",
        [
            new ChainBlock("C", [new Effort("Clean", 1, Bell24), new Effort("Press", 1, Bell24)],
                Cycles: 5, Sets: 3, NinetySeconds),
        ]));

        // Same totals, different set to perform. Tonnage cannot tell them apart, which is correct:
        // the difference is in how the work is ordered, not in how much of it there is.
        chain.PrescribedTonnage.ShouldBe(complex.PrescribedTonnage);
        chain.PrescribedTonnage.ShouldBe(720m);
    }

    [Fact]
    public void A_chain_describes_its_cycling_even_though_the_totals_match()
    {
        var resolved = ResolveSingle(new ChainBlock("Chain",
        [
            new Effort("Clean", 1, Bell24),
            new Effort("Press", 1, Bell24),
        ], Cycles: 5, Sets: 1, NinetySeconds));

        // The description is the only place the difference survives, so it has to carry it.
        resolved.Steps.Single().Description.ShouldContain("5 × (");
    }
}
