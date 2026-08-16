using Ironbell.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ironbell.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real API against a throwaway container database. Only the provider and connection
/// string are overridden — everything else is the production composition root, so a slice test
/// exercises the same wiring that ships.
/// </summary>
public sealed class IronbellApiFactory(DatabaseProvider provider, string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("Database:Provider", provider.ToString());
        builder.UseSetting("ConnectionStrings:Ironbell", connectionString);
    }
}
