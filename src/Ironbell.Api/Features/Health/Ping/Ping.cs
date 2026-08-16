using Ironbell.Api.Common.Messaging;
using Ironbell.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ironbell.Api.Features.Health.Ping;

/// <summary>
/// The reference slice. Request, response, handler, DI registration and endpoint all live in this
/// one file — that colocation is the pattern every later slice copies.
/// </summary>
public sealed record PingRequest : IRequest<PingResponse>;

/// <param name="Status">Always <c>ok</c>; a failure surfaces as a non-200 instead.</param>
/// <param name="Utc">Server clock. UTC <see cref="DateTime"/>, never <see cref="DateTimeOffset"/>,
/// per ADR 0001.</param>
/// <param name="SchemaVersion">Read from the database, so a green ping proves the round trip
/// rather than just that the process is alive.</param>
public sealed record PingResponse(string Status, DateTime Utc, string SchemaVersion);

internal sealed class PingHandler(TimeProvider timeProvider, IronbellDbContext dbContext)
    : IHandler<PingRequest, PingResponse>
{
    public async ValueTask<PingResponse> HandleAsync(
        PingRequest request,
        CancellationToken cancellationToken)
    {
        var schemaVersion = await dbContext.AppInfo
            .AsNoTracking()
            .Select(appInfo => appInfo.SchemaVersion)
            .FirstAsync(cancellationToken);

        return new PingResponse("ok", timeProvider.GetUtcNow().UtcDateTime, schemaVersion);
    }
}

internal static class PingSlice
{
    internal static IServiceCollection AddPing(this IServiceCollection services)
    {
        services.AddScoped<IHandler<PingRequest, PingResponse>, PingHandler>();
        return services;
    }

    internal static IEndpointRouteBuilder MapPing(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/health/ping",
                async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
                    TypedResults.Ok(
                        await dispatcher.SendAsync<PingRequest, PingResponse>(
                            new PingRequest(),
                            cancellationToken)))
            .WithName("HealthPing")
            .WithTags("Health");

        return endpoints;
    }
}
