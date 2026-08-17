using Ironbell.Domain.Training;

namespace Ironbell.Domain.Tests.Training;

/// <summary>
/// M2's check for tonnage and personal records: the numbers must match a computation done by hand.
/// </summary>
/// <remarks>
/// The arithmetic is written out in the comments deliberately. A test that asserts a total the code
/// produced is circular — it only proves the code is consistent with itself. These figures were
/// worked out independently, so the test can actually disagree with the implementation.
/// </remarks>
public class HandComputedCheckTests
{
    private static readonly BellWeight Bell24 = new(24);

    private static DateTime March(int day) => new(2026, 3, day, 6, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A week in the shape of Rite of Passage: a press ladder, swings, snatches, and one
    /// double-bell movement to exercise the bell count.
    /// </summary>
    private static List<LoggedSet> TrainingWeek()
    {
        var sets = new List<LoggedSet>();

        // Day 1 — press ladder 1-2-3, five rounds @ 24 kg.
        // 1 + 2 + 3 = 6 reps a round; 6 × 5 = 30 reps; 30 × 24 = 720 kg
        for (var round = 0; round < 5; round++)
        {
            foreach (var rung in (int[])[1, 2, 3])
            {
                sets.Add(new LoggedSet("Press", rung, Bell24, March(2)));
            }
        }

        // Day 1 — swings, ten sets of ten @ 24 kg.
        // 10 × 10 = 100 reps; 100 × 24 = 2400 kg
        for (var set = 0; set < 10; set++)
        {
            sets.Add(new LoggedSet("Swing", 10, Bell24, March(2)));
        }

        // Day 3 — snatches, five sets of ten @ 24 kg.
        // 5 × 10 = 50 reps; 50 × 24 = 1200 kg
        for (var set = 0; set < 5; set++)
        {
            sets.Add(new LoggedSet("Snatch", 10, Bell24, March(4)));
        }

        // Day 3 — double front squats, three sets of five @ 24 kg in each hand.
        // 3 × 5 = 15 reps; 15 × 24 = 360 kg on one bell; × 2 bells = 720 kg
        for (var set = 0; set < 3; set++)
        {
            sets.Add(new LoggedSet("Front squat", 5, Bell24, March(4), Bells: 2));
        }

        return sets;
    }

    [Fact]
    public void Weekly_tonnage_matches_the_hand_computation()
    {
        // 720 (press) + 2400 (swing) + 1200 (snatch) + 720 (double front squat) = 5040 kg
        Tonnage.Of(TrainingWeek()).ShouldBe(5040m);
    }

    [Fact]
    public void Daily_tonnage_matches_the_hand_computation()
    {
        var byDay = Tonnage.ByDay(TrainingWeek());

        byDay[new DateOnly(2026, 3, 2)].ShouldBe(3120m);   // 720 + 2400
        byDay[new DateOnly(2026, 3, 4)].ShouldBe(1920m);   // 1200 + 720
        byDay.Count.ShouldBe(2);
    }

    [Fact]
    public void The_double_bell_movement_is_a_fifth_of_the_snatch_volume_not_a_tenth()
    {
        var byDay = Tonnage.ByDay(TrainingWeek());

        // Guards the specific error Bells exists to prevent. Counted as a single bell the squats
        // would be 360 kg and the day would total 1560 kg, which is wrong by 360 kg and looks
        // entirely plausible.
        byDay[new DateOnly(2026, 3, 4)].ShouldNotBe(1560m);
    }

    [Fact]
    public void Personal_records_match_the_hand_computation()
    {
        var records = PersonalRecords.From(TrainingWeek());

        // Best single set of each movement at that load:
        //   Front squat, 24 kg, two bells — 5
        //   Press,       24 kg, one bell  — 3   (the top rung, not the 30 total reps)
        //   Snatch,      24 kg, one bell  — 10
        //   Swing,       24 kg, one bell  — 10
        records.Select(record => (record.Exercise, record.Bells, record.Reps))
            .ShouldBe(
            [
                ("Front squat", 2, 5),
                ("Press", 1, 3),
                ("Snatch", 1, 10),
                ("Swing", 1, 10),
            ]);
    }

    [Fact]
    public void A_record_is_a_single_set_and_never_a_session_total()
    {
        var press = PersonalRecords.From(TrainingWeek())
            .Single(record => record.Exercise == "Press");

        // Thirty presses were done that day. The record is three, because that is the most that
        // happened without putting the bell down.
        press.Reps.ShouldBe(3);
        press.AchievedAtUtc.ShouldBe(March(2));
    }
}
