using Ironbell.Domain;
using Ironbell.Infrastructure.Conversions;
using Microsoft.EntityFrameworkCore;

namespace Ironbell.Infrastructure;

public sealed class IronbellDbContext(DbContextOptions<IronbellDbContext> options) : DbContext(options)
{
    public DbSet<AppInfo> AppInfo => Set<AppInfo>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        BumpConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Applied model-wide rather than per property, so a new entity cannot forget it.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IronbellDbContext).Assembly);
    }

    /// <summary>
    /// An <see cref="int"/> token is portable precisely because no provider maintains it, so the
    /// increment has to happen here. Writing to OriginalValue + 1 means a stale in-memory entity
    /// still produces the WHERE clause that detects the conflict.
    /// </summary>
    private void BumpConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<IConcurrencyTracked>())
        {
            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            var token = entry.Property(nameof(IConcurrencyTracked.ConcurrencyToken));
            token.CurrentValue = (int)(token.OriginalValue ?? 0) + 1;
        }
    }
}
