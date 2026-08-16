namespace Ironbell.Client.Common;

internal static class ApiHttpClient
{
    /// <summary>
    /// Named client for the Ironbell API. The base address is the app's own origin because the
    /// published client is served from the API container — same origin, so no CORS story to design
    /// and the refresh cookie stays first-party.
    /// </summary>
    internal const string Name = "Ironbell.Api";
}
