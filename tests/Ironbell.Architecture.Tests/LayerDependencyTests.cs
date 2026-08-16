using System.Reflection;

namespace Ironbell.Architecture.Tests;

/// <summary>
/// Keeps the project graph pointing one way and keeps <c>Ironbell.Domain</c> free of frameworks.
/// </summary>
public class LayerDependencyTests
{
    /// <summary>
    /// Everything Domain is allowed to reference. Deliberately tiny — Domain is shared with the
    /// WASM client, so anything that lands here also lands in the browser payload and has to
    /// survive trimming.
    /// </summary>
    private static readonly string[] DomainAllowlist =
    [
        "System.Runtime",
        "System.Collections",
        "System.Linq",
        "System.Memory",
        "netstandard",
        "System.Private.CoreLib",
    ];

    private static Assembly DomainAssembly => typeof(Domain.AssemblyMarker).Assembly;

    private static Assembly InfrastructureAssembly => typeof(Infrastructure.AssemblyMarker).Assembly;

    [Fact]
    public void Domain_references_nothing_outside_the_allowlist()
    {
        // An allowlist rather than a blocklist on purpose: a blocklist only catches the frameworks
        // someone thought to forbid, and the whole point is that Domain stays pure against
        // dependencies nobody has thought of yet.
        var disallowed = DomainAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !DomainAllowlist.Contains(name, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        disallowed.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Serilog")]
    [InlineData("Ironbell.Infrastructure")]
    [InlineData("Ironbell.Api")]
    public void Domain_does_not_reference(string forbidden)
    {
        // Redundant against the allowlist, and kept anyway: when this fails the message names the
        // thing that crept in, which the allowlist test cannot do as clearly.
        DomainAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ShouldNotContain(forbidden);
    }

    [Fact]
    public void Infrastructure_does_not_reference_the_api()
    {
        // Infrastructure is a detail the host consumes, never the other way round.
        InfrastructureAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ShouldNotContain("Ironbell.Api");
    }

    [Fact]
    public void Infrastructure_references_the_domain()
    {
        InfrastructureAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ShouldContain("Ironbell.Domain");
    }
}
