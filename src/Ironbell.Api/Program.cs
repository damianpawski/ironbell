var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Ironbell API");

app.Run();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the API in slice tests.
/// Top-level statements otherwise compile this as internal.
/// </summary>
public partial class Program;
