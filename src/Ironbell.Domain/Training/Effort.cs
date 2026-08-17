namespace Ironbell.Domain.Training;

/// <summary>
/// One movement, performed on one or two bells.
/// </summary>
/// <param name="Exercise">Movement name.</param>
/// <param name="Reps">
/// Prescribed repetitions, or <see langword="null"/> when the plan does not set a count — timed
/// work is whatever the athlete manages inside the window.
/// </param>
/// <param name="Weight">The weight of a single bell.</param>
/// <param name="Bells">
/// How many bells are used at once. Snatches take one; front squats often take two.
/// </param>
/// <remarks>
/// <see cref="Bells"/> exists because <see cref="Weight"/> is the weight of one bell, not of the
/// load. A double 24 kg front squat moves twice what a single one does, and counting it once would
/// halve the tonnage of every double-bell movement ever logged. That is not an error a later fix
/// repairs, because the history would already be wrong.
/// </remarks>
public sealed record Effort(string Exercise, int? Reps, BellWeight Weight, int Bells = 1)
{
    public int Bells { get; init; } = Bells > 0
        ? Bells
        : throw new ArgumentOutOfRangeException(nameof(Bells), Bells, "An effort uses at least one bell.");

    /// <summary>Weight moved by performing this effort as prescribed; zero when uncounted.</summary>
    public decimal PrescribedTonnage => Reps is { } reps ? Weight.TonnageFor(reps) * Bells : 0m;
}
