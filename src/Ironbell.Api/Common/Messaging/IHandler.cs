namespace Ironbell.Api.Common.Messaging;

/// <summary>
/// Handles exactly one request type. Implementations live inside their own feature slice and are
/// never referenced from another slice — shared logic belongs in <c>Ironbell.Domain</c>.
/// </summary>
/// <typeparam name="TRequest">The request this handler answers.</typeparam>
/// <typeparam name="TResponse">The type returned to the caller.</typeparam>
public interface IHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
