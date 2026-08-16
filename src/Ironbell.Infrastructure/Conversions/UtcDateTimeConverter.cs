using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ironbell.Infrastructure.Conversions;

/// <summary>
/// Forces every <see cref="DateTime"/> to UTC on the way in and stamps it back as UTC on the way
/// out.
/// </summary>
/// <remarks>
/// Without this the two providers disagree in a way no compiler catches. SQL Server's
/// <c>datetime2</c> carries no zone, so a value read back has <see cref="DateTimeKind.Unspecified"/>;
/// Npgsql's <c>timestamp with time zone</c> returns <see cref="DateTimeKind.Utc"/>. Identical code
/// over an identical model would therefore behave differently per provider — exactly the silent rot
/// ADR 0001 is trying to prevent.
/// </remarks>
internal sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            written => written.Kind == DateTimeKind.Utc ? written : written.ToUniversalTime(),
            read => DateTime.SpecifyKind(read, DateTimeKind.Utc))
    {
    }
}
