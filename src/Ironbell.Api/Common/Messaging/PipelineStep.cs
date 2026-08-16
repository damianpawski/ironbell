namespace Ironbell.Api.Common.Messaging;

/// <summary>
/// The next step in the pipeline: either the following behaviour or, at the end of the chain,
/// the handler itself.
/// </summary>
/// <typeparam name="TResponse">The type returned to the caller.</typeparam>
public delegate ValueTask<TResponse> PipelineStep<TResponse>();
