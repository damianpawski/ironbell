namespace Ironbell.Domain.Tests;

public class AppInfoTests
{
    [Theory]
    [InlineData("Ironbell", "ironbell")]
    [InlineData("IRONBELL", "ironbell")]
    [InlineData("  Ironbell  ", "ironbell")]
    public void Normalise_lowercases_and_trims(string input, string expected)
    {
        AppInfo.Normalise(input).ShouldBe(expected);
    }

    [Fact]
    public void Construction_derives_the_normalised_name()
    {
        var appInfo = new AppInfo("IronBell", "m0", DateTime.UtcNow);

        appInfo.Name.ShouldBe("IronBell");
        appInfo.NameNormalised.ShouldBe("ironbell");
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void A_non_utc_timestamp_is_refused(DateTimeKind kind)
    {
        var notUtc = DateTime.SpecifyKind(new DateTime(2026, 8, 16, 12, 0, 0), kind);

        // ADR 0001 stores UTC DateTime and nothing else. Catching it at construction beats
        // discovering it once two providers have disagreed about what the value meant.
        Should.Throw<ArgumentException>(() => new AppInfo("Ironbell", "m0", notUtc));
    }

    [Fact]
    public void A_utc_timestamp_is_accepted()
    {
        var utc = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

        var appInfo = new AppInfo("Ironbell", "m0", utc);

        appInfo.SeededAtUtc.ShouldBe(utc);
        appInfo.SeededAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_is_refused(string blank)
    {
        Should.Throw<ArgumentException>(() => new AppInfo(blank, "m0", DateTime.UtcNow));
    }

    [Fact]
    public void Recording_a_schema_version_replaces_it()
    {
        var appInfo = new AppInfo("Ironbell", "m0", DateTime.UtcNow);

        appInfo.RecordSchemaVersion("m1");

        appInfo.SchemaVersion.ShouldBe("m1");
    }
}
