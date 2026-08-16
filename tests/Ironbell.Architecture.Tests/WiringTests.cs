using System.Reflection;

namespace Ironbell.Architecture.Tests;

public class WiringTests
{
    public static TheoryData<Assembly, string> ProjectsUnderTest() => new()
    {
        { typeof(Domain.AssemblyMarker).Assembly, "Ironbell.Domain" },
        { typeof(Infrastructure.AssemblyMarker).Assembly, "Ironbell.Infrastructure" },
        { typeof(Program).Assembly, "Ironbell.Api" },
    };

    [Theory]
    [MemberData(nameof(ProjectsUnderTest))]
    public void Assembly_under_test_is_reachable(Assembly assembly, string expectedName)
    {
        assembly.GetName().Name.ShouldBe(expectedName);
    }
}
