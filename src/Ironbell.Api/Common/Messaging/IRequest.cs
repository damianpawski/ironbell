namespace Ironbell.Api.Common.Messaging;

/// <summary>
/// Marker for a request that a single <see cref="IHandler{TRequest, TResponse}"/> answers with
/// <typeparamref name="TResponse"/>. Carrying the response type on the request is what lets the
/// dispatcher stay reflection-free.
/// </summary>
/// <typeparam name="TResponse">The type the handler returns.</typeparam>
public interface IRequest<TResponse>;
