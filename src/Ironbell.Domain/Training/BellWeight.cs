namespace Ironbell.Domain.Training;

/// <summary>
/// A bell's weight in kilograms.
/// </summary>
/// <remarks>
/// A type rather than a loose decimal because it is multiplied by reps to produce tonnage, which is
/// the headline metric of the whole app. Getting a unit wrong there would be silent and wrong
/// everywhere at once.
/// </remarks>
public readonly record struct BellWeight
{
    public BellWeight(decimal kilograms)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(kilograms);

        Kilograms = kilograms;
    }

    public decimal Kilograms { get; }

    /// <summary>Total weight moved by performing <paramref name="reps"/> repetitions.</summary>
    public decimal TonnageFor(int reps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reps);

        return Kilograms * reps;
    }

    public override string ToString() => $"{Kilograms:0.##} kg";
}
