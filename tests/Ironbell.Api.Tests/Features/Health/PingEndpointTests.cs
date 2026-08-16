using System.Net;
using System.Net.Http.Json;
using Ironbell.Api.Features.Health.Ping;
using Ironbell.Api.Tests.Infrastructure;

namespace Ironbell.Api.Tests.Features.Health;

/// <summary>
/// Slice test: hits the real endpoint through the real pipeline against a real database. For VSA
/// this is the primary test type, not an afterthought.
/// </summary>
[Collection(SharedDatabase.Name)]
public sealed class PingEndpointTests(DatabaseFixture database)
{
    private static readonly Uri PingRoute = new("/api/health/ping", UriKind.Relative);

    [Fact]
    public async Task Ping_returns_200_with_ok_status()
    {
        using var client = database.Factory.CreateClient();

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
        using var client = database.Factory.CreateClient();

        var body = await client.GetFromJsonAsync<PingResponse>(
            PingRoute,
            TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Utc.Kind.ShouldBe(DateTimeKind.Utc);
        body.Utc.ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Ping_reports_the_schema_version_read_from_the_database()
    {
        using var client = database.Factory.CreateClient();

        var body = await client.GetFromJsonAsync<PingResponse>(
            PingRoute,
            TestContext.Current.CancellationToken);

        // A green ping proves the database round trip, not merely that the process is alive.
        body.ShouldNotBeNull();
        body.SchemaVersion.ShouldBe("m0");
    }
}
