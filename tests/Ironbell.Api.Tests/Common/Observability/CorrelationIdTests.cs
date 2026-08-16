using Ironbell.Api.Common.Observability;
using Ironbell.Api.Tests.Infrastructure;

namespace Ironbell.Api.Tests.Common.Observability;

[Collection(SharedDatabase.Name)]
public sealed class CorrelationIdTests(DatabaseFixture database)
{
    private static readonly Uri PingRoute = new("/api/health/ping", UriKind.Relative);

    [Fact]
    public async Task A_well_formed_client_id_is_adopted_and_echoed_back()
    {
        const string Supplied = "abc123-DEF456";

        var returned = await GetCorrelationIdAsync(Supplied);

        returned.ShouldBe(Supplied);
    }

    [Fact]
    public async Task An_id_is_generated_when_the_client_sends_none()
    {
        var returned = await GetCorrelationIdAsync(supplied: null);

        returned.ShouldNotBeNullOrWhiteSpace();
        CorrelationId.IsWellFormed(returned).ShouldBeTrue();
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("semi;colon")]
    [InlineData("quote\"mark")]
    [InlineData("under_score")]
    public async Task A_malformed_client_id_is_replaced_rather_than_reflected(string malformed)
    {
        var returned = await GetCorrelationIdAsync(malformed);

        returned.ShouldNotBe(malformed);
        CorrelationId.IsWellFormed(returned).ShouldBeTrue();
    }

    [Fact]
    public async Task An_overlong_client_id_is_replaced()
    {
        var overlong = new string('a', 65);

        var returned = await GetCorrelationIdAsync(overlong);

        returned.ShouldNotBe(overlong);
        CorrelationId.IsWellFormed(returned).ShouldBeTrue();
    }

    [Fact]
    public void Newlines_are_rejected_so_log_lines_cannot_be_forged()
    {
        CorrelationId.IsWellFormed("safe\nINFO forged log line").ShouldBeFalse();
        CorrelationId.IsWellFormed("safe\r\nINFO forged log line").ShouldBeFalse();
    }

    private async Task<string> GetCorrelationIdAsync(string? supplied)
    {
        using var client = database.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, PingRoute);

        if (supplied is not null)
        {
            request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, supplied);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.Headers.GetValues(CorrelationId.HeaderName).ShouldHaveSingleItem();
    }
}
