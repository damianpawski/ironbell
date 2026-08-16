using Ironbell.Api.Common.Messaging;
using Ironbell.Api.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMessaging();
builder.Services.AddFeatures();

var app = builder.Build();

app.MapFeatures();

app.Run();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the API in slice tests.
/// Top-level statements otherwise compile this as internal.
/// </summary>
public partial class Program;
