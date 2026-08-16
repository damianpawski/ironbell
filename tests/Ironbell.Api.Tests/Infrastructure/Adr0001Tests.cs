using Ironbell.Domain;
using Ironbell.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ironbell.Api.Tests.Infrastructure;

/// <summary>
/// The rules from ADR 0001, asserted against whichever provider this run is pointed at.
/// </summary>
/// <remarks>
/// These are the tests that make "provider-portable" a checked claim rather than an intention. Run
/// on PostgreSQL by default and on SQL Server in the CI matrix, so a rule that holds on one
/// provider and not the other fails a build instead of surfacing at migration time.
/// </remarks>
[Collection(SharedDatabase.Name)]
public sealed class Adr0001Tests(DatabaseFixture database)
{
    [Fact]
    public void Tables_and_columns_are_snake_case()
    {
        using var scope = database.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IronbellDbContext>();

        var entityType = dbContext.Model.FindEntityType(typeof(AppInfo)).ShouldNotBeNull();

        entityType.GetTableName().ShouldBe("app_info");
        entityType.GetProperty(nameof(AppInfo.NameNormalised))
            .GetColumnName()
            .ShouldBe("name_normalised");
        entityType.GetProperty(nameof(AppInfo.SeededAtUtc))
            .GetColumnName()
            .ShouldBe("seeded_at_utc");
    }

    [Fact]
    public async Task Utc_timestamps_come_back_as_utc_on_either_provider()
    {
        var probe = await AddProbeAsync();

        using var scope = database.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IronbellDbContext>();

        var reloaded = await dbContext.AppInfo
            .AsNoTracking()
            .SingleAsync(row => row.Id == probe.Id, TestContext.Current.CancellationToken);

        // Without the UTC converter this passes on Npgsql and fails on SQL Server.
        reloaded.SeededAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Concurrency_token_increments_on_update()
    {
        var probe = await AddProbeAsync();

        using var scope = database.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IronbellDbContext>();

        var tracked = await dbContext.AppInfo
            .SingleAsync(row => row.Id == probe.Id, TestContext.Current.CancellationToken);
        var before = tracked.ConcurrencyToken;

        tracked.RecordSchemaVersion("m0-updated");
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        tracked.ConcurrencyToken.ShouldBe(before + 1);
    }

    [Fact]
    public async Task A_stale_write_is_rejected_rather_than_silently_winning()
    {
        var probe = await AddProbeAsync();

        using var firstScope = database.Factory.Services.CreateScope();
        using var secondScope = database.Factory.Services.CreateScope();

        var firstContext = firstScope.ServiceProvider.GetRequiredService<IronbellDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<IronbellDbContext>();

        var first = await firstContext.AppInfo
            .SingleAsync(row => row.Id == probe.Id, TestContext.Current.CancellationToken);
        var second = await secondContext.AppInfo
            .SingleAsync(row => row.Id == probe.Id, TestContext.Current.CancellationToken);

        first.RecordSchemaVersion("winner");
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        second.RecordSchemaVersion("loser");

        await Should.ThrowAsync<DbUpdateConcurrencyException>(async () =>
            await secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Uniqueness_is_case_insensitive_because_it_is_normalised_in_code()
    {
        var name = $"Probe-{Guid.NewGuid():N}";

        await AddProbeAsync(name);

        using var scope = database.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IronbellDbContext>();

        // Differs only by case. SQL Server would collide on Name by collation and PostgreSQL would
        // not; both collide on the normalised column, which is the whole point of having one.
        dbContext.AppInfo.Add(new AppInfo(name.ToUpperInvariant(), "duplicate", DateTime.UtcNow));

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private async Task<AppInfo> AddProbeAsync(string? name = null)
    {
        using var scope = database.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IronbellDbContext>();

        var probe = new AppInfo(name ?? $"Probe-{Guid.NewGuid():N}", "probe", DateTime.UtcNow);

        dbContext.AppInfo.Add(probe);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return probe;
    }
}
