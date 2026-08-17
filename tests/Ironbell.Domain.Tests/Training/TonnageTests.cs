using Ironbell.Domain.Training;

namespace Ironbell.Domain.Tests.Training;

public class TonnageTests
{
    private static readonly BellWeight Bell24 = new(24);
    private static readonly BellWeight Bell32 = new(32);
    private static readonly DateTime March4 = new(2026, 3, 4, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Tonnage_is_weight_times_reps_summed()
    {
        LoggedSet[] sets =
        [
            new("Swing", 10, Bell24, March4),
            new("Swing", 10, Bell24, March4),
            new("Snatch", 5, Bell32, March4),
        ];

        // 240 + 240 + 160
        Tonnage.Of(sets).ShouldBe(640m);
    }

    [Fact]
    public void Two_bells_move_twice_the_weight()
    {
        var single = new LoggedSet("Front squat", 5, Bell24, March4);
        var doubled = new LoggedSet("Front squat", 5, Bell24, March4, Bells: 2);

        // The reason Bells exists. Counting a double 24 as a single one would halve the tonnage of
        // every double-bell movement, and the history would already be wrong by the time anyone
        // noticed.
        single.Tonnage.ShouldBe(120m);
        doubled.Tonnage.ShouldBe(240m);
    }

    [Fact]
    public void An_abandoned_set_moves_nothing_but_is_still_a_set()
    {
        var abandoned = new LoggedSet("Press", 0, Bell32, March4);

        abandoned.Tonnage.ShouldBe(0m);
    }

    [Fact]
    public void Negative_reps_are_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new LoggedSet("Press", -1, Bell24, March4));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void A_non_utc_completion_time_is_refused(DateTimeKind kind)
    {
        var notUtc = DateTime.SpecifyKind(new DateTime(2026, 3, 4, 6, 0, 0), kind);

        Should.Throw<ArgumentException>(() => new LoggedSet("Press", 5, Bell24, notUtc));
    }

    [Fact]
    public void By_day_groups_on_the_utc_date()
    {
        LoggedSet[] sets =
        [
            new("Swing", 10, Bell24, new DateTime(2026, 3, 4, 6, 0, 0, DateTimeKind.Utc)),
            new("Swing", 10, Bell24, new DateTime(2026, 3, 4, 19, 0, 0, DateTimeKind.Utc)),
            new("Swing", 10, Bell24, new DateTime(2026, 3, 5, 6, 0, 0, DateTimeKind.Utc)),
        ];

        var byDay = Tonnage.ByDay(sets);

        byDay[new DateOnly(2026, 3, 4)].ShouldBe(480m);
        byDay[new DateOnly(2026, 3, 5)].ShouldBe(240m);
    }

    [Fact]
    public void A_day_without_training_is_absent_rather_than_zero()
    {
        LoggedSet[] sets = [new("Swing", 10, Bell24, March4)];

        // The density grid dims a missed day. It has to tell "did not train" apart from "trained
        // and moved nothing", and a zero would erase that difference.
        Tonnage.ByDay(sets).ContainsKey(new DateOnly(2026, 3, 5)).ShouldBeFalse();
    }

    [Fact]
    public void Tonnage_of_nothing_is_zero()
    {
        Tonnage.Of([]).ShouldBe(0m);
    }
}
