using Ironbell.Domain.Training;

namespace Ironbell.Domain.Tests.Training;

public class BellWeightTests
{
    [Theory]
    [InlineData(24, 10, 240)]
    [InlineData(32, 5, 160)]
    [InlineData(16, 0, 0)]
    public void Tonnage_is_weight_times_reps(decimal kilograms, int reps, decimal expected)
    {
        new BellWeight(kilograms).TonnageFor(reps).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_bell_must_weigh_something(decimal kilograms)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BellWeight(kilograms));
    }

    [Fact]
    public void Negative_reps_are_refused()
    {
        // Tonnage is the headline metric, so a negative contribution must never be representable.
        Should.Throw<ArgumentOutOfRangeException>(() => new BellWeight(24).TonnageFor(-1));
    }

    [Fact]
    public void Half_kilo_bells_survive_the_round_trip()
    {
        // decimal rather than double: 0.5 kg increments exist, and tonnage sums thousands of reps.
        new BellWeight(4.5m).TonnageFor(3).ShouldBe(13.5m);
    }
}
