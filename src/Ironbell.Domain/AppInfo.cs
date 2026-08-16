namespace Ironbell.Domain;

/// <summary>
/// Deployment metadata for the running instance. One row, read by the health endpoint so a deploy
/// can be confirmed to be talking to the database it thinks it is.
/// </summary>
public sealed class AppInfo : IConcurrencyTracked
{
    private AppInfo()
    {
        // EF materialisation.
    }

    public AppInfo(string name, string schemaVersion, DateTime seededAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);

        if (seededAtUtc.Kind is not DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Timestamps are stored as UTC DateTime, never DateTimeOffset (ADR 0001).",
                nameof(seededAtUtc));
        }

        Name = name;
        NameNormalised = Normalise(name);
        SchemaVersion = schemaVersion;
        SeededAtUtc = seededAtUtc;
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>
    /// Lowercased copy of <see cref="Name"/>, and the column uniqueness is enforced on.
    /// </summary>
    /// <remarks>
    /// SQL Server collates case-insensitively and PostgreSQL does not, so a uniqueness rule that
    /// leans on collation passes on one provider and fails on the other. Normalising in code takes
    /// the database's opinion out of it entirely.
    /// </remarks>
    public string NameNormalised { get; private set; } = null!;

    public string SchemaVersion { get; private set; } = null!;

    /// <summary>UTC. Never <see cref="DateTimeOffset"/> — see ADR 0001.</summary>
    public DateTime SeededAtUtc { get; private set; }

    public int ConcurrencyToken { get; private set; }

    /// <summary>
    /// Records the schema version a deployment has brought the database up to.
    /// </summary>
    public void RecordSchemaVersion(string schemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);

        SchemaVersion = schemaVersion;
    }

    public static string Normalise(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Trim().ToLowerInvariant();
    }
}
