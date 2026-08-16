namespace Ironbell.Api.Common.Messaging;

internal static class MessagingRegistration
{
    /// <summary>
    /// Registers the dispatcher. Handlers register themselves inside their own slice; behaviours
    /// are added as open generics so the container closes them per request type.
    /// </summary>
    internal static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        return services;
    }
}
