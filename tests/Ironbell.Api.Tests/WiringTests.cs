namespace Ironbell.Api.Tests;

public class WiringTests
{
    [Fact]
    public void Api_assembly_is_referenced()
    {
        typeof(Program).Assembly.GetName().Name.ShouldBe("Ironbell.Api");
    }
}
