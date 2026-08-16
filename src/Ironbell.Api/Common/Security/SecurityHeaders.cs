namespace Ironbell.Api.Common.Security;

/// <summary>
/// Response headers applied to everything the host serves.
/// </summary>
/// <remarks>
/// On from day one deliberately. The gap triage files CSP as Tier-A because retrofitting it breaks
/// things in ways that are hard to find — a policy added once an app is large fails in a corner
/// nobody visits until a user does.
/// </remarks>
internal static class SecurityHeaders
{
    /// <summary>
    /// Default-deny, opened only where Blazor genuinely requires it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>'wasm-unsafe-eval'</c> is required: the .NET runtime compiles WebAssembly at load, which
    /// the CSP spec treats as an eval. It is far narrower than <c>'unsafe-eval'</c> — it permits
    /// WebAssembly compilation and nothing else, so JavaScript <c>eval</c> stays blocked.
    /// </para>
    /// <para>
    /// <c>connect-src 'self'</c> is what makes the same-origin decision enforceable rather than
    /// merely intended: the client physically cannot call another origin.
    /// </para>
    /// <para>
    /// There are no script hashes and no <c>'unsafe-inline'</c> because the shell carries no inline
    /// scripts at all — see <c>OverrideHtmlAssetPlaceholders</c> in the client project. Deriving
    /// hashes automatically from whatever happened to be inline was tried and rejected: it would
    /// bless any inline script that later appeared, which is precisely what this policy exists to
    /// prevent.
    /// </para>
    /// </remarks>
    private static readonly string ContentSecurityPolicy = string.Join("; ",
    [
        "default-src 'self'",
        "script-src 'self' 'wasm-unsafe-eval'",
        "style-src 'self'",
        "img-src 'self' data:",
        "font-src 'self'",
        "connect-src 'self'",
        "manifest-src 'self'",
        "worker-src 'self'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
        "frame-ancestors 'none'",
        "upgrade-insecure-requests",
    ]);

    internal static IApplicationBuilder UseIronbellSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";

            // The app never needs these; denying them shrinks what a compromised script could do.
            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=()";

            // frame-ancestors already covers this for modern browsers. Kept for older ones, since
            // an installed PWA may run in an old Android WebView.
            headers.XFrameOptions = "DENY";

            await next();
        });
    }
}
