using System.Diagnostics;

namespace Ironbell.Api.Common.Messaging.Behaviours;

/// <summary>
/// Logs the name and duration of every handled request.
/// </summary>
/// <remarks>
/// Only the request <em>type name</em> is ever logged, never the request itself. That makes
/// credentials and tokens structurally incapable of reaching a log line, rather than relying on
/// every slice to remember to redact them.
/// </remarks>
internal sealed class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehaviour<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        PipelineStep<TResponse> nextStep,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nextStep);

        var requestName = typeof(TRequest).Name;
        var startedAt = Stopwatch.GetTimestamp();

        LoggingBehaviourLog.Handling(logger, requestName);

        try
        {
            var response = await nextStep();

            var elapsed = Elapsed(startedAt);
            LoggingBehaviourLog.Handled(logger, requestName, elapsed);

            return response;
        }
        catch (Exception exception)
        {
            var elapsed = Elapsed(startedAt);
            LoggingBehaviourLog.Failed(logger, exception, requestName, elapsed);

            throw;
        }
    }

    private static double Elapsed(long startedAt) =>
        Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 1);
}

/// <summary>
/// Source-generated log delegates. Declared on a non-generic type so the generator does not have to
/// close over the behaviour's type parameters.
/// </summary>
internal static partial class LoggingBehaviourLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling {RequestName}")]
    internal static partial void Handling(ILogger logger, string requestName);

    // Debug, not Information. Serilog's request logging already records method, path, status and
    // duration at Information, so an Information line here was a second entry saying much the same
    // thing. It also mattered more once probes existed: a liveness check every few seconds would
    // have made handler timing the loudest thing in the log.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Handled {RequestName} in {ElapsedMilliseconds} ms")]
    internal static partial void Handled(ILogger logger, string requestName, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "{RequestName} failed after {ElapsedMilliseconds} ms")]
    internal static partial void Failed(ILogger logger, Exception exception, string requestName, double elapsedMilliseconds);
}
