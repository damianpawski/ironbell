namespace Ironbell.Domain.Training;

/// <summary>
/// The most reps ever completed in a single set of a given movement at a given load, and when.
/// </summary>
/// <param name="Exercise">Movement name, as first recorded.</param>
/// <param name="Weight">Weight of a single bell.</param>
/// <param name="Bells">How many bells.</param>
/// <param name="Reps">The record.</param>
/// <param name="AchievedAtUtc">When it was first achieved.</param>
/// <remarks>
/// Dated, and that is the whole design: "Best 32 kg snatch set: 24 reps, 4 March". Beating it is
/// the reward, so there is no badge and nothing to collect.
/// </remarks>
public sealed record PersonalRecord(
    string Exercise,
    BellWeight Weight,
    int Bells,
    int Reps,
    DateTime AchievedAtUtc);

public static class PersonalRecords
{
    /// <summary>
    /// Reduces logged sets to one record per movement, load and bell count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A record is only broken by strictly more reps. Equalling it changes nothing, and the date
    /// stays at the first time the number was reached — that is when it was achieved. Treating a
    /// tie as a new record would manufacture a fresh congratulation out of repeating yesterday's
    /// work, which is exactly the dopamine loop the product sets out to avoid.
    /// </para>
    /// <para>
    /// Load is part of the key rather than something to compare across. Twenty reps at 24 kg and
    /// twelve at 32 kg are different achievements and neither supersedes the other.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<PersonalRecord> From(IEnumerable<LoggedSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);

        var best = new Dictionary<(string Exercise, decimal Kilograms, int Bells), PersonalRecord>();

        foreach (var set in sets)
        {
            // Case-insensitive so "Snatch" and "snatch" are one movement rather than two records.
            var key = (set.Exercise.ToLowerInvariant(), set.Weight.Kilograms, set.Bells);

            if (!best.TryGetValue(key, out var current))
            {
                best[key] = new PersonalRecord(
                    set.Exercise, set.Weight, set.Bells, set.Reps, set.CompletedAtUtc);
                continue;
            }

            if (set.Reps > current.Reps)
            {
                best[key] = new PersonalRecord(
                    set.Exercise, set.Weight, set.Bells, set.Reps, set.CompletedAtUtc);
            }
            else if (set.Reps == current.Reps && set.CompletedAtUtc < current.AchievedAtUtc)
            {
                // Same number, seen earlier: the record is older than we thought, not newer.
                best[key] = current with { AchievedAtUtc = set.CompletedAtUtc };
            }
        }

        // Deterministic order, so a golden file over these stays stable.
        return [.. best.Values
            .OrderBy(record => record.Exercise, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Weight.Kilograms)
            .ThenBy(record => record.Bells)];
    }
}
