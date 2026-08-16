namespace Ironbell.Api.Common.Observability;

/// <summary>
/// Names and validation for the id that ties one client action to its server-side log lines.
/// </summary>
internal static class CorrelationId
{
    internal const string HeaderName = "X-Correlation-Id";
    internal const string LogPropertyName = "CorrelationId";
    internal const string ItemKey = "Ironbell.CorrelationId";

    private const int MaxLength = 64;

    internal static string New() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// A client-supplied id is written straight into log output, so it is only trusted when it is
    /// short and strictly alphanumeric. Anything else — a newline above all — could be used to
    /// forge log lines, so it is discarded in favour of a fresh id rather than sanitised.
    /// </summary>
    internal static bool IsWellFormed(string? candidate) =>
        !string.IsNullOrEmpty(candidate)
        && candidate.Length <= MaxLength
        && candidate.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-');
}
