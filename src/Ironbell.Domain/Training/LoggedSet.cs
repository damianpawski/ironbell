namespace Ironbell.Domain.Training;

/// <summary>
/// A set that was actually performed.
/// </summary>
/// <param name="Exercise">Movement name.</param>
/// <param name="Reps">Repetitions completed. Zero is legitimate — a set can be abandoned.</param>
/// <param name="Weight">The weight of a single bell.</param>
/// <param name="CompletedAtUtc">When the set finished. UTC, per ADR 0001.</param>
/// <param name="Bells">How many bells were used at once.</param>
/// <remarks>
/// Distinct from <see cref="Effort"/> on purpose. An effort is what a plan asks for; a logged set is
/// what happened. Tonnage and personal records are computed from what happened and never from what
/// was planned — otherwise a programme nobody followed would still report progress.
/// </remarks>
public sealed record LoggedSet(
    string Exercise,
    int Reps,
    BellWeight Weight,
    DateTime CompletedAtUtc,
    int Bells = 1)
{
    public DateTime CompletedAtUtc { get; init; } = CompletedAtUtc.Kind is DateTimeKind.Utc
        ? CompletedAtUtc
        : throw new ArgumentException(
            "Timestamps are stored as UTC DateTime, never DateTimeOffset (ADR 0001).",
            nameof(CompletedAtUtc));

    public int Reps { get; init; } = Reps >= 0
        ? Reps
        : throw new ArgumentOutOfRangeException(nameof(Reps), Reps, "Reps cannot be negative.");

    public int Bells { get; init; } = Bells > 0
        ? Bells
        : throw new ArgumentOutOfRangeException(nameof(Bells), Bells, "A set uses at least one bell.");

    /// <summary>Weight actually moved by this set.</summary>
    public decimal Tonnage => Weight.TonnageFor(Reps) * Bells;
}
