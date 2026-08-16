using Ironbell.Api.Features.Health.Ping;

namespace Ironbell.Api.Features;

/// <summary>
/// Composition root for the slices. Every slice is listed here by hand rather than discovered by
/// assembly scanning: the wiring stays greppable, trimming stays predictable, and no slice is ever
/// activated by accident. This is the one file allowed to reference every slice.
/// </summary>
internal static class FeatureRegistration
{
    internal static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        services.AddPing();
        return services;
    }

    internal static IEndpointRouteBuilder MapFeatures(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPing();
        return endpoints;
    }
}
