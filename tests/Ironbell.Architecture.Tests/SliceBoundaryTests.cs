using System.Reflection;
using NetArchTest.Rules;

namespace Ironbell.Architecture.Tests;

/// <summary>
/// The rule that keeps Vertical Slice Architecture from decaying into a tangle: a slice never
/// reaches into another slice. Shared logic belongs in <c>Ironbell.Domain</c>.
/// </summary>
/// <remarks>
/// This is a build failure rather than a convention because cross-slice coupling is cheap to
/// prevent on day one and a large refactor once it has spread.
/// </remarks>
public class SliceBoundaryTests
{
    private const string FeaturesRoot = "Ironbell.Api.Features";

    private static Assembly ApiAssembly => typeof(Program).Assembly;

    /// <summary>
    /// A slice is any namespace beneath <c>Features</c> that actually holds types.
    /// </summary>
    private static IReadOnlyList<string> Slices() =>
        [.. ApiAssembly.GetTypes()
            .Select(type => type.Namespace)
            .Where(name => name is not null
                && name.StartsWith(FeaturesRoot + ".", StringComparison.Ordinal))
            .Select(name => name!)
            .Distinct()
            .Order(StringComparer.Ordinal)];

    [Fact]
    public void Slice_discovery_finds_at_least_one_slice()
    {
        // Guards the tests below. If discovery ever returned nothing — a rename of the Features
        // namespace would do it — every cross-slice assertion would pass vacuously and go on
        // passing forever while the rule it protects quietly stopped being enforced.
        Slices().ShouldNotBeEmpty();
    }

    [Fact]
    public void No_slice_depends_on_another_slice()
    {
        var slices = Slices();

        var violations = new List<string>();

        foreach (var slice in slices)
        {
            var otherSlices = slices.Where(other => other != slice).ToArray();
            if (otherSlices.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(ApiAssembly)
                .That().ResideInNamespace(slice)
                .ShouldNot().HaveDependencyOnAny(otherSlices)
                .GetResult();

            if (!result.IsSuccessful)
            {
                violations.AddRange(
                    (result.FailingTypes ?? [])
                    .Select(type => $"{type.FullName} reaches outside {slice}"));
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Only_the_composition_root_sits_directly_under_Features()
    {
        // The slice rule above works precisely because slices live in deeper namespaces than
        // FeatureRegistration, so the composition root is exempt by position rather than by a
        // special case. Anything else appearing at this level would silently gain that exemption.
        var typesAtFeaturesRoot = ApiAssembly.GetTypes()
            .Where(type => string.Equals(type.Namespace, FeaturesRoot, StringComparison.Ordinal))
            .Select(type => type.Name)
            .Where(name => !name.StartsWith('<'))
            .ToList();

        typesAtFeaturesRoot.ShouldBe(["FeatureRegistration"]);
    }

    [Fact]
    public void Handlers_live_inside_a_slice()
    {
        var handlersOutsideSlices = ApiAssembly.GetTypes()
            .Where(type => type.GetInterfaces().Any(contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition().Name.StartsWith("IHandler", StringComparison.Ordinal)))
            .Where(type => type.Namespace?.StartsWith(FeaturesRoot + ".", StringComparison.Ordinal) != true)
            .Select(type => type.FullName)
            .ToList();

        handlersOutsideSlices.ShouldBeEmpty();
    }
}
