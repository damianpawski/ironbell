using Ironbell.Api.Common.Messaging.Behaviours;

namespace Ironbell.Api.Common.Messaging;

internal static class MessagingRegistration
{
    /// <summary>
    /// Registers the dispatcher and the behaviour pipeline. Handlers register themselves inside
    /// their own slice; behaviours go in as open generics so the container closes them per request
    /// type. Registration order is pipeline order — the first added ends up outermost.
    /// </summary>
    internal static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(LoggingBehaviour<,>));
        return services;
    }
}
