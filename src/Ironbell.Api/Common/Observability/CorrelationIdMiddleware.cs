using Serilog.Context;

namespace Ironbell.Api.Common.Observability;

/// <summary>
/// Adopts the caller's correlation id, or mints one, and keeps it on the log context for the whole
/// request. Echoing it back lets the client tie its own log lines to the server's.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var incoming = context.Request.Headers[CorrelationId.HeaderName].ToString();
        var correlationId = CorrelationId.IsWellFormed(incoming) ? incoming : CorrelationId.New();

        context.Items[CorrelationId.ItemKey] = correlationId;
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;

        using (LogContext.PushProperty(CorrelationId.LogPropertyName, correlationId))
        {
            await next(context);
        }
    }
}

internal static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Must run before request logging so the completion log line carries the id too.
    /// </summary>
    internal static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
