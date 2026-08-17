using Ironbell.Domain.Training;

namespace Ironbell.Domain.Tests.Training;

public class PersonalRecordsTests
{
    private static readonly BellWeight Bell24 = new(24);
    private static readonly BellWeight Bell32 = new(32);

    private static DateTime March(int day) => new(2026, 3, day, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_record_is_the_most_reps_in_a_single_set()
    {
        LoggedSet[] sets =
        [
            new("Snatch", 20, Bell32, March(1)),
            new("Snatch", 24, Bell32, March(4)),
            new("Snatch", 22, Bell32, March(8)),
        ];

        var record = PersonalRecords.From(sets).Single();

        // "Best 32 kg snatch set: 24 reps, 4 March."
        record.Reps.ShouldBe(24);
        record.AchievedAtUtc.ShouldBe(March(4));
    }

    [Fact]
    public void Equalling_a_record_does_not_break_it()
    {
        LoggedSet[] sets =
        [
            new("Snatch", 24, Bell32, March(4)),
            new("Snatch", 24, Bell32, March(11)),
        ];

        var record = PersonalRecords.From(sets).Single();

        // Repeating yesterday's work is not an achievement, and manufacturing a congratulation out
        // of it is exactly the dopamine loop the product avoids. The date stays at first achieved.
        record.AchievedAtUtc.ShouldBe(March(4));
    }

    [Fact]
    public void An_earlier_equal_set_moves_the_date_back_not_forward()
    {
        // Sets can arrive out of order, e.g. a backfilled session syncing after a later one.
        LoggedSet[] sets =
        [
            new("Snatch", 24, Bell32, March(11)),
            new("Snatch", 24, Bell32, March(4)),
        ];

        PersonalRecords.From(sets).Single().AchievedAtUtc.ShouldBe(March(4));
    }

    [Fact]
    public void Different_loads_are_different_records()
    {
        LoggedSet[] sets =
        [
            new("Snatch", 40, Bell24, March(4)),
            new("Snatch", 24, Bell32, March(4)),
        ];

        var records = PersonalRecords.From(sets);

        // Forty at 24 kg does not supersede twenty-four at 32 kg. Neither is "the" snatch record.
        records.Count.ShouldBe(2);
        records.Select(record => record.Reps).ShouldBe([40, 24]);
    }

    [Fact]
    public void One_bell_and_two_are_different_records()
    {
        LoggedSet[] sets =
        [
            new("Front squat", 10, Bell24, March(4)),
            new("Front squat", 10, Bell24, March(4), Bells: 2),
        ];

        PersonalRecords.From(sets).Count.ShouldBe(2);
    }

    [Fact]
    public void Casing_does_not_split_a_movement_into_two_records()
    {
        LoggedSet[] sets =
        [
            new("Snatch", 20, Bell32, March(1)),
            new("snatch", 24, Bell32, March(4)),
        ];

        var record = PersonalRecords.From(sets).Single();

        record.Reps.ShouldBe(24);
    }

    [Fact]
    public void Records_come_back_in_a_stable_order()
    {
        LoggedSet[] sets =
        [
            new("Snatch", 24, Bell32, March(4)),
            new("Clean", 10, Bell24, March(4)),
            new("Snatch", 40, Bell24, March(4)),
        ];

        // Deterministic, so a golden file over these stays reviewable as a diff.
        PersonalRecords.From(sets)
            .Select(record => $"{record.Exercise} {record.Weight}")
            .ShouldBe(["Clean 24 kg", "Snatch 24 kg", "Snatch 32 kg"]);
    }

    [Fact]
    public void No_sets_means_no_records()
    {
        PersonalRecords.From([]).ShouldBeEmpty();
    }
}
