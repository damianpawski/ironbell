using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ironbell.Infrastructure;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>. Never used at runtime.
/// </summary>
/// <remarks>
/// Pinned to SQL Server because migrations are generated for production only. PostgreSQL builds its
/// schema from the same model via EnsureCreated in the CI test fixture, so it needs no migration
/// set of its own — EF cannot hold two providers' migrations for one context in one assembly.
/// Scaffolding a migration does not connect, so this connection string is never opened.
/// </remarks>
public sealed class IronbellDbContextFactory : IDesignTimeDbContextFactory<IronbellDbContext>
{
    public IronbellDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IronbellDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ironbell_design_time")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new IronbellDbContext(options);
    }
}
