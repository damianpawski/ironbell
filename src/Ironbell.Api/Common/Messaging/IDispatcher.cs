namespace Ironbell.Api.Common.Messaging;

/// <summary>
/// Sends a request through the behaviour pipeline to its handler.
/// </summary>
/// <remarks>
/// Both type arguments are explicit at the call site. That is deliberate: naming the closed
/// generic lets the container resolve <see cref="IHandler{TRequest, TResponse}"/> directly, so
/// dispatch needs no reflection and nothing has to survive trimming by name.
/// </remarks>
public interface IDispatcher
{
    ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>;
}
