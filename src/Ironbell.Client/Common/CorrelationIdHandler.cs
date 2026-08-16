namespace Ironbell.Client.Common;

/// <summary>
/// Stamps every outgoing API call with a fresh correlation id and logs both ends of the exchange,
/// so a line in the browser console can be matched to the server's lines for the same call.
/// </summary>
internal sealed class CorrelationIdHandler(ILogger<CorrelationIdHandler> logger) : DelegatingHandler
{
    /// <summary>Must match <c>Ironbell.Api.Common.Observability.CorrelationId.HeaderName</c>.</summary>
    internal const string HeaderName = "X-Correlation-Id";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = Guid.NewGuid().ToString("N");
        request.Headers.TryAddWithoutValidation(HeaderName, correlationId);

        CorrelationIdHandlerLog.Sending(logger, request.Method.Method, request.RequestUri, correlationId);

        var response = await base.SendAsync(request, cancellationToken);

        CorrelationIdHandlerLog.Received(
            logger,
            request.Method.Method,
            request.RequestUri,
            (int)response.StatusCode,
            correlationId);

        return response;
    }
}

internal static partial class CorrelationIdHandlerLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{Method} {RequestUri} {CorrelationId}")]
    internal static partial void Sending(ILogger logger, string method, Uri? requestUri, string correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Method} {RequestUri} responded {StatusCode} {CorrelationId}")]
    internal static partial void Received(ILogger logger, string method, Uri? requestUri, int statusCode, string correlationId);
}
