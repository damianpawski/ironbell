namespace Ironbell.Domain.Training;

/// <summary>
/// Total weight moved — the headline metric.
/// </summary>
/// <remarks>
/// Blunt and quantitative on purpose. The product philosophy puts tonnage first precisely because
/// it cannot be gamed into feeling good: it is the weight, times the reps, times the bells, and
/// nothing else.
/// </remarks>
public static class Tonnage
{
    /// <summary>Weight moved by the given sets, in kilograms.</summary>
    public static decimal Of(IEnumerable<LoggedSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);

        return sets.Sum(set => set.Tonnage);
    }

    /// <summary>
    /// Weight moved per calendar day, in UTC.
    /// </summary>
    /// <remarks>
    /// Days with no work are absent rather than zero: the 28-day density grid dims a cell for a day
    /// that was missed, and it needs to tell "no training" apart from "training that moved nothing".
    /// </remarks>
    public static IReadOnlyDictionary<DateOnly, decimal> ByDay(IEnumerable<LoggedSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);

        return sets
            .GroupBy(set => DateOnly.FromDateTime(set.CompletedAtUtc))
            .ToDictionary(day => day.Key, day => day.Sum(set => set.Tonnage));
    }
}
