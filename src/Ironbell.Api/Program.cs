using System.Globalization;
using Ironbell.Api.Common.Messaging;
using Ironbell.Api.Common.Observability;
using Ironbell.Api.Common.Security;
using Ironbell.Api.Features;
using Ironbell.Infrastructure;
using Serilog;
using Serilog.Events;

// Bootstrap logger, so anything that fails before configuration is read still reaches the console.
// Deliberately CreateLogger and not CreateBootstrapLogger: the latter installs a ReloadableLogger
// on the process-wide Log.Logger, which the host then freezes. Slice tests build several hosts in
// one process, and the second freeze throws. A plain logger makes that failure impossible instead
// of asking every future test class to share one WebApplicationFactory.
Log.Logger = new LoggerConfiguration()
    // Invariant so log output does not vary with the host's locale.
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddSingleton(TimeProvider.System);

// Migrations are applied as a pipeline step, never on startup — see the M0 build list. The app
// assumes the schema is already there.
builder.Services.AddIronbellDatabase(
    builder.Configuration.GetValue("Database:Provider", DatabaseProvider.SqlServer),
    builder.Configuration.GetConnectionString("Ironbell")
        ?? throw new InvalidOperationException(
            "Connection string 'Ironbell' is not configured. Locally this comes from user-secrets."));

builder.Services.AddMessaging();
builder.Services.AddFeatures();
builder.Services.AddOpenApi();

// No CORS, on purpose. The client is served from this host, so every call the browser makes is
// same-origin. Registering a policy would create the cross-origin surface the architecture exists
// to avoid, and `connect-src 'self'` in the CSP would refuse it anyway. If a second origin is ever
// genuinely needed, that is a decision to take deliberately rather than a default left switched on.

var app = builder.Build();

// First in the pipeline so error responses and static files carry the headers too.
app.UseIronbellSecurityHeaders();

// Order matters: the correlation id has to be on the log context before request logging closes
// its completion line, otherwise that line is the one entry missing the id.
app.UseCorrelationId();

app.UseSerilogRequestLogging(options =>
{
    // Health probes are the highest-frequency traffic this app will ever see and the least
    // interesting. Logged at Verbose so they stay available when debugging and invisible otherwise,
    // rather than burying real requests. Failures still surface at Error whatever the route.
    options.GetLevel = static (httpContext, _, exception) =>
        exception is not null || httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : httpContext.Request.Path.StartsWithSegments("/api/health")
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
});

// Same origin as the API, in development as well as in the container, so the two never diverge.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapFeatures();

if (app.Environment.IsDevelopment())
{
    // Development only: the schema describes the whole API surface, which is not something to
    // publish for a single-user app.
    app.MapOpenApi();
}

// Client-side routes are not API routes: anything unmatched hands back the WASM shell.
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the API in slice tests.
/// Top-level statements otherwise compile this as internal.
/// </summary>
public partial class Program;
