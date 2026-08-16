namespace Ironbell.Domain.Tests;

public class WiringTests
{
    [Fact]
    public void Domain_assembly_is_referenced()
    {
        typeof(AssemblyMarker).Assembly.GetName().Name.ShouldBe("Ironbell.Domain");
    }
}
