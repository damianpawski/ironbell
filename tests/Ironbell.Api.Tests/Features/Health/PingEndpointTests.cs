using System.Net;
using System.Net.Http.Json;
using Ironbell.Api.Features.Health.Ping;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ironbell.Api.Tests.Features.Health;

/// <summary>
/// Slice test: hits the real endpoint through the real pipeline. For VSA this is the primary
/// test type, not an afterthought.
/// </summary>
public sealed class PingEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Uri PingRoute = new("/api/health/ping", UriKind.Relative);

    [Fact]
    public async Task Ping_returns_200_with_ok_status()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(PingRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PingResponse>(
            TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Status.ShouldBe("ok");
    }

    [Fact]
    public async Task Ping_reports_a_utc_timestamp()
    {
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<PingResponse>(
            PingRoute,
            TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Utc.Kind.ShouldBe(DateTimeKind.Utc);
        body.Utc.ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }
}
