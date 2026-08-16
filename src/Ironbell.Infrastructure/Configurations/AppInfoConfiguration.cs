using Ironbell.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ironbell.Infrastructure.Configurations;

internal sealed class AppInfoConfiguration : IEntityTypeConfiguration<AppInfo>
{
    /// <summary>The single seeded row. Fixed values so the migration is deterministic.</summary>
    private static readonly DateTime SeededAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<AppInfo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Table and column names are left to the snake_case convention rather than spelled out.
        // Hardcoding them here would hide a convention that had stopped working.
        builder.HasKey(appInfo => appInfo.Id);

        builder.Property(appInfo => appInfo.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(appInfo => appInfo.NameNormalised)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(appInfo => appInfo.SchemaVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(appInfo => appInfo.SeededAtUtc)
            .IsRequired();

        builder.Property(appInfo => appInfo.ConcurrencyToken)
            .IsConcurrencyToken();

        // Uniqueness is on the normalised column, never on Name — SQL Server would compare it
        // case-insensitively and PostgreSQL would not.
        builder.HasIndex(appInfo => appInfo.NameNormalised)
            .IsUnique();

        builder.HasData(new
        {
            Id = 1,
            Name = "Ironbell",
            NameNormalised = "ironbell",
            SchemaVersion = "m0",
            SeededAtUtc,
            ConcurrencyToken = 0,
        });
    }
}
