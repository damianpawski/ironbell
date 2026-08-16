namespace Ironbell.Api.Common.Messaging;

/// <inheritdoc cref="IDispatcher"/>
internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);

        var handler = services.GetRequiredService<IHandler<TRequest, TResponse>>();

        PipelineStep<TResponse> next = () => handler.HandleAsync(request, cancellationToken);

        // Wrap inside-out so the first-registered behaviour ends up outermost.
        var behaviours = services.GetServices<IPipelineBehaviour<TRequest, TResponse>>().ToArray();
        for (var i = behaviours.Length - 1; i >= 0; i--)
        {
            var behaviour = behaviours[i];
            var inner = next;
            next = () => behaviour.HandleAsync(request, inner, cancellationToken);
        }

        return next();
    }
}
