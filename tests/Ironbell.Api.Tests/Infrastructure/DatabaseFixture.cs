using DotNet.Testcontainers.Containers;
using Ironbell.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Ironbell.Api.Tests.Infrastructure;

/// <summary>
/// Starts one throwaway database container for the whole test assembly.
/// </summary>
/// <remarks>
/// The provider comes from the IRONBELL_TEST_PROVIDER environment variable and defaults to
/// PostgreSQL. That default is deliberate: production is SQL Server, so PostgreSQL is the side that
/// would rot unnoticed, and ADR 0001 names the dual-provider run as the only real defence. CI runs
/// the matrix over both.
/// </remarks>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string ProviderVariable = "IRONBELL_TEST_PROVIDER";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";
    private const string PostgresImage = "postgres:17-alpine";

    private IDatabaseContainer? _container;

    public DatabaseProvider Provider { get; } = ResolveProvider();

    public IronbellApiFactory Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // Images pinned to the same tags compose.yaml uses locally, so a test run and a dev run
        // are not quietly on different engine versions.
        _container = Provider switch
        {
            DatabaseProvider.SqlServer => new MsSqlBuilder(SqlServerImage).Build(),
            DatabaseProvider.Postgres => new PostgreSqlBuilder(PostgresImage).Build(),
            _ => throw new InvalidOperationException($"Unsupported provider '{Provider}'."),
        };

        await _container.StartAsync();

        Factory = new IronbellApiFactory(Provider, _container.GetConnectionString());

        await CreateSchemaAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// SQL Server runs the real migrations, because that is what production applies. PostgreSQL
    /// builds the same model with EnsureCreated — EF cannot hold two providers' migrations for one
    /// context in one assembly, and the PostgreSQL run exists to catch model and query rot rather
    /// than to rehearse DDL that nothing will ever apply.
    /// </summary>
    private async Task CreateSchemaAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IronbellDbContext>();

        if (Provider is DatabaseProvider.SqlServer)
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
    }

    private static DatabaseProvider ResolveProvider()
    {
        var configured = Environment.GetEnvironmentVariable(ProviderVariable);

        return string.IsNullOrWhiteSpace(configured)
            ? DatabaseProvider.Postgres
            : Enum.Parse<DatabaseProvider>(configured, ignoreCase: true);
    }
}

/// <summary>
/// Binds every database-backed test class to the one container started per assembly.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SharedDatabase : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
