using Ironbell.Client;
using Ironbell.Client.Common;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.BrowserConsole(
        outputTemplate: "[{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(dispose: true);

builder.Services.AddTransient<CorrelationIdHandler>();

builder.Services
    .AddHttpClient(
        ApiHttpClient.Name,
        client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<CorrelationIdHandler>();

builder.Services.AddScoped(serviceProvider =>
    serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(ApiHttpClient.Name));

await builder.Build().RunAsync();
