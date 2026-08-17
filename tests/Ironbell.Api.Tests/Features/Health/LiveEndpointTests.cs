using System.Net;
using System.Net.Http.Json;
using Ironbell.Api.Features.Health.Live;
using Ironbell.Api.Tests.Infrastructure;
using Ironbell.Infrastructure;

namespace Ironbell.Api.Tests.Features.Health;

/// <summary>
/// Liveness and readiness must disagree when the database is down. These tests need no container:
/// the point is precisely that nothing reachable is required.
/// </summary>
public sealed class LiveEndpointTests
{
    private static readonly Uri LiveRoute = new("/api/health/live", UriKind.Relative);
    private static readonly Uri PingRoute = new("/api/health/ping", UriKind.Relative);

    /// <summary>Port 1, where nothing listens. Short timeout so the failure is quick.</summary>
    private const string UnreachableDatabase =
        "Server=tcp:127.0.0.1,1;Initial Catalog=nowhere;User ID=none;Password=none;"
        + "Encrypt=False;TrustServerCertificate=True;Connection Timeout=1;";

    [Fact]
    public async Task Liveness_answers_even_when_the_database_is_unreachable()
    {
        using var factory = new IronbellApiFactory(DatabaseProvider.SqlServer, UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(LiveRoute, TestContext.Current.CancellationToken);

        // This is the assertion that keeps a database pause from restarting healthy containers.
        // Azure SQL's serverless tier pauses on idle by design, so this is routine, not an edge.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LiveResponse>(
            TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Status.ShouldBe("live");
    }

    [Fact]
    public async Task Readiness_does_not_report_ok_when_the_database_is_unreachable()
    {
        using var factory = new IronbellApiFactory(DatabaseProvider.SqlServer, UnreachableDatabase);
        using var client = factory.CreateClient();

        HttpStatusCode? status = null;

        try
        {
            using var response = await client.GetAsync(PingRoute, TestContext.Current.CancellationToken);
            status = response.StatusCode;
        }
        catch (Exception)
        {
            // TestServer surfaces an unhandled exception to the caller instead of turning it into a
            // 500, so either outcome is acceptable. What matters is that it never reports healthy.
        }

        status.ShouldNotBe(HttpStatusCode.OK);
    }
}
