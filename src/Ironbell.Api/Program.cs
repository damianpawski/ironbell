using Ironbell.Api.Common.Messaging;
using Ironbell.Api.Common.Observability;
using Ironbell.Api.Features;
using Ironbell.Infrastructure;
using Serilog;

// Bootstrap logger, so anything that fails before configuration is read still reaches the console.
// Deliberately CreateLogger and not CreateBootstrapLogger: the latter installs a ReloadableLogger
// on the process-wide Log.Logger, which the host then freezes. Slice tests build several hosts in
// one process, and the second freeze throws. A plain logger makes that failure impossible instead
// of asking every future test class to share one WebApplicationFactory.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
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

var app = builder.Build();

// Order matters: the correlation id has to be on the log context before request logging closes
// its completion line, otherwise that line is the one entry missing the id.
app.UseCorrelationId();
app.UseSerilogRequestLogging();

app.MapFeatures();

app.Run();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the API in slice tests.
/// Top-level statements otherwise compile this as internal.
/// </summary>
public partial class Program;
