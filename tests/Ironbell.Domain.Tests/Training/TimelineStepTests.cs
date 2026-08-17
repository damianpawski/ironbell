using Ironbell.Domain.Training;

namespace Ironbell.Domain.Tests.Training;

public class TimelineStepTests
{
    private static readonly BellWeight Bell24 = new(24);

    [Fact]
    public void A_step_with_a_duration_is_owned_by_the_clock()
    {
        var rest = new TimelineStep(1, StepKind.Rest, "Rest 90 s", [], TimeSpan.FromSeconds(90));

        rest.IsTimed.ShouldBeTrue();
    }

    [Fact]
    public void A_step_without_a_duration_waits_for_the_athlete()
    {
        var work = new TimelineStep(
            0, StepKind.Work, "5 × Press @ 24 kg", [new Effort("Press", 5, Bell24)], null);

        // The distinction the whole timeline rests on: nothing invents a length for a set of
        // presses, so no screen can pretend the session is running to a schedule.
        work.IsTimed.ShouldBeFalse();
    }

    [Fact]
    public void Prescribed_tonnage_comes_from_reps_and_weight()
    {
        var work = new TimelineStep(
            0, StepKind.Work, "10 × Swing @ 24 kg", [new Effort("Swing", 10, Bell24)], null);

        work.PrescribedTonnage.ShouldBe(240m);
    }

    [Fact]
    public void A_step_spanning_several_movements_counts_all_of_them()
    {
        // This is the case that forced a step to hold a list. A complex is one unbroken effort
        // across several movements, and counting only the first would understate the headline
        // metric by however many movements followed it.
        var complex = new TimelineStep(0, StepKind.Work, "clean + press + squat",
        [
            new Effort("Clean", 5, Bell24),
            new Effort("Press", 5, Bell24),
            new Effort("Squat", 5, Bell24),
        ], null);

        complex.PrescribedTonnage.ShouldBe(360m);
    }

    [Fact]
    public void Uncounted_work_contributes_no_tonnage()
    {
        // Timed work prescribes no reps, so there is nothing honest to add.
        var timed = new TimelineStep(0, StepKind.Work, "Snatch @ 24 kg for 30 s",
            [new Effort("Snatch", null, Bell24)], TimeSpan.FromSeconds(30));

        timed.PrescribedTonnage.ShouldBe(0m);
    }

    [Fact]
    public void Rest_contributes_no_tonnage()
    {
        var rest = new TimelineStep(1, StepKind.Rest, "Rest", [], TimeSpan.FromMinutes(1));

        rest.PrescribedTonnage.ShouldBe(0m);
    }
}
