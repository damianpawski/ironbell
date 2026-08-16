using System.Text.RegularExpressions;
using Ironbell.Api.Tests.Infrastructure;

namespace Ironbell.Api.Tests.Common.Security;

[Collection(SharedDatabase.Name)]
public sealed partial class SecurityHeadersTests(DatabaseFixture database)
{
    private static readonly Uri PingRoute = new("/api/health/ping", UriKind.Relative);
    private static readonly Uri Shell = new("/", UriKind.Relative);

    [GeneratedRegex(
        """<script\b(?![^>]*\bsrc\s*=)[^>]*>(.*?)</script>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineScriptPattern { get; }

    [Theory]
    [InlineData("Content-Security-Policy")]
    [InlineData("X-Content-Type-Options")]
    [InlineData("Referrer-Policy")]
    [InlineData("Permissions-Policy")]
    [InlineData("X-Frame-Options")]
    public async Task Security_headers_are_present(string header)
    {
        using var client = database.Factory.CreateClient();

        using var response = await client.GetAsync(PingRoute, TestContext.Current.CancellationToken);

        response.Headers.Contains(header).ShouldBeTrue($"'{header}' was missing.");
    }

    [Theory]
    [InlineData("default-src 'self'")]
    [InlineData("script-src 'self' 'wasm-unsafe-eval'")]
    [InlineData("object-src 'none'")]
    [InlineData("frame-ancestors 'none'")]
    [InlineData("base-uri 'self'")]
    [InlineData("connect-src 'self'")]
    public async Task Policy_contains_directive(string directive)
    {
        var policy = await GetPolicyAsync();

        policy.ShouldContain(directive);
    }

    [Theory]
    [InlineData("'unsafe-inline'")]
    [InlineData("'unsafe-eval'")]
    public async Task Policy_never_relaxes_to(string forbidden)
    {
        var policy = await GetPolicyAsync();

        // 'wasm-unsafe-eval' is present and permits WebAssembly compilation only. Full
        // 'unsafe-eval' and 'unsafe-inline' would give a script-injection a way to execute.
        policy.ShouldNotContain(forbidden);
    }

    [Fact]
    public async Task The_shell_carries_no_inline_scripts()
    {
        using var client = database.Factory.CreateClient();

        var html = await client.GetStringAsync(Shell, TestContext.Current.CancellationToken);

        // The strict policy allows no inline script by hash or nonce, so one appearing in the shell
        // would leave the app blank in a browser while every server-side test still passed. This is
        // the test that would have caught it.
        var inlineScripts = InlineScriptPattern.Matches(html)
            .Where(match => !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            .ToList();

        inlineScripts.ShouldBeEmpty();
    }

    [Fact]
    public async Task No_cors_headers_are_emitted_for_a_cross_origin_request()
    {
        using var client = database.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, PingRoute);
        request.Headers.TryAddWithoutValidation("Origin", "https://not-ironbell.example");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Deliberate: the client is served from this host, so nothing is cross-origin. An
        // Access-Control-Allow-Origin appearing here would mean a CORS policy had been added,
        // which is a decision to take consciously rather than inherit.
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private async Task<string> GetPolicyAsync()
    {
        using var client = database.Factory.CreateClient();

        using var response = await client.GetAsync(PingRoute, TestContext.Current.CancellationToken);

        return response.Headers.GetValues("Content-Security-Policy").Single();
    }
}
