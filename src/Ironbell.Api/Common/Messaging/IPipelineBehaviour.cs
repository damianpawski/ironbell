namespace Ironbell.Api.Common.Messaging;

/// <summary>
/// Cross-cutting step wrapped around every handler — logging, validation, authorisation.
/// Behaviours run in registration order on the way in and unwind in reverse on the way out.
/// </summary>
/// <typeparam name="TRequest">The request being handled.</typeparam>
/// <typeparam name="TResponse">The type returned to the caller.</typeparam>
public interface IPipelineBehaviour<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        PipelineStep<TResponse> nextStep,
        CancellationToken cancellationToken);
}
