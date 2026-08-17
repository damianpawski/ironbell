using Ironbell.Domain.Training;

namespace Ironbell.Domain.Tests.Training;

public class TimelineStepTests
{
    [Fact]
    public void A_step_with_a_duration_is_owned_by_the_clock()
    {
        var rest = new TimelineStep(
            Ordinal: 1,
            Kind: StepKind.Rest,
            Description: "Rest 90 s",
            Exercise: null,
            Reps: null,
            Weight: null,
            Duration: TimeSpan.FromSeconds(90));

        rest.IsTimed.ShouldBeTrue();
    }

    [Fact]
    public void A_step_without_a_duration_waits_for_the_athlete()
    {
        var work = new TimelineStep(
            Ordinal: 0,
            Kind: StepKind.Work,
            Description: "5 presses @ 24 kg",
            Exercise: "Press",
            Reps: 5,
            Weight: new BellWeight(24),
            Duration: null);

        // The distinction the whole timeline rests on: nothing invents a length for a set of
        // presses, so no screen can pretend the session is running to a schedule.
        work.IsTimed.ShouldBeFalse();
    }

    [Fact]
    public void Prescribed_tonnage_comes_from_reps_and_weight()
    {
        var work = new TimelineStep(0, StepKind.Work, "10 swings @ 24 kg", "Swing", 10, new BellWeight(24), null);

        work.PrescribedTonnage.ShouldBe(240m);
    }

    [Fact]
    public void Rest_contributes_no_tonnage()
    {
        var rest = new TimelineStep(1, StepKind.Rest, "Rest", null, null, null, TimeSpan.FromMinutes(1));

        rest.PrescribedTonnage.ShouldBe(0m);
    }
}
