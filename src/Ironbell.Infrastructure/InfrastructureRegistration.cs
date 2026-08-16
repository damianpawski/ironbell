using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ironbell.Infrastructure;

public static class InfrastructureRegistration
{
    /// <summary>
    /// Registers <see cref="IronbellDbContext"/> against the given provider.
    /// </summary>
    /// <remarks>
    /// Provider and connection string are passed in rather than read from IConfiguration, so this
    /// project keeps no opinion about where configuration comes from.
    /// </remarks>
    public static IServiceCollection AddIronbellDatabase(
        this IServiceCollection services,
        DatabaseProvider provider,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<IronbellDbContext>(options =>
        {
            switch (provider)
            {
                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(
                        connectionString,
                        sqlServer => sqlServer.EnableRetryOnFailure());
                    break;

                case DatabaseProvider.Postgres:
                    options.UseNpgsql(
                        connectionString,
                        npgsql => npgsql.EnableRetryOnFailure());
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(provider),
                        provider,
                        "Unsupported database provider.");
            }

            // snake_case for tables and columns on both providers. Keeping PostgreSQL's convention
            // while running on SQL Server costs nothing now and removes a rename from the eventual
            // migration.
            options.UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
