namespace Ironbell.Domain.Training;

/// <summary>
/// One movement performed on one bell.
/// </summary>
/// <param name="Exercise">Movement name.</param>
/// <param name="Reps">
/// Prescribed repetitions, or <see langword="null"/> when the plan does not set a count — timed
/// work is whatever the athlete manages inside the window.
/// </param>
/// <param name="Weight">The bell.</param>
public sealed record Effort(string Exercise, int? Reps, BellWeight Weight)
{
    /// <summary>Weight moved by performing this effort as prescribed; zero when uncounted.</summary>
    public decimal PrescribedTonnage => Reps is { } reps ? Weight.TonnageFor(reps) : 0m;
}
