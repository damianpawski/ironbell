using Ironbell.Api.Common.Messaging;

namespace Ironbell.Api.Features.Health.Live;

/// <summary>
/// Liveness: is this process running and able to serve a request.
/// </summary>
/// <remarks>
/// Deliberately touches nothing. Liveness and readiness answer different questions, and conflating
/// them is actively harmful: a liveness probe that reads the database turns a brief database pause
/// into a restart of perfectly healthy containers, which is an outage manufactured out of a blip.
/// Azure SQL's serverless tier pauses after idle by design, so this is a certainty rather than a
/// risk. <c>/api/health/ping</c> is the readiness signal; this is the liveness one.
/// </remarks>
public sealed record LiveRequest : IRequest<LiveResponse>;

/// <param name="Status">Always <c>live</c>. Anything else would mean the process could not answer,
/// in which case there is no response at all.</param>
public sealed record LiveResponse(string Status);

internal sealed class LiveHandler : IHandler<LiveRequest, LiveResponse>
{
    private static readonly LiveResponse Response = new("live");

    public ValueTask<LiveResponse> HandleAsync(
        LiveRequest request,
        CancellationToken cancellationToken) => ValueTask.FromResult(Response);
}

internal static class LiveSlice
{
    internal const string Route = "/api/health/live";

    internal static IServiceCollection AddLive(this IServiceCollection services)
    {
        services.AddScoped<IHandler<LiveRequest, LiveResponse>, LiveHandler>();
        return services;
    }

    internal static IEndpointRouteBuilder MapLive(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                Route,
                async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
                    TypedResults.Ok(
                        await dispatcher.SendAsync<LiveRequest, LiveResponse>(
                            new LiveRequest(),
                            cancellationToken)))
            .WithName("HealthLive")
            .WithTags("Health");

        return endpoints;
    }
}
