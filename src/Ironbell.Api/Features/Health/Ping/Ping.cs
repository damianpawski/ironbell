using Ironbell.Api.Common.Messaging;

namespace Ironbell.Api.Features.Health.Ping;

/// <summary>
/// The reference slice. Request, response, handler, DI registration and endpoint all live in this
/// one file — that colocation is the pattern every later slice copies.
/// </summary>
public sealed record PingRequest : IRequest<PingResponse>;

/// <param name="Status">Always <c>ok</c>; a failure surfaces as a non-200 instead.</param>
/// <param name="Utc">Server clock. UTC <see cref="DateTime"/>, never <see cref="DateTimeOffset"/>,
/// per ADR 0001.</param>
public sealed record PingResponse(string Status, DateTime Utc);

internal sealed class PingHandler(TimeProvider timeProvider)
    : IHandler<PingRequest, PingResponse>
{
    public ValueTask<PingResponse> HandleAsync(
        PingRequest request,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new PingResponse("ok", timeProvider.GetUtcNow().UtcDateTime));
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
