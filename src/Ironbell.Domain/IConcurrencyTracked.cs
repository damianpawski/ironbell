namespace Ironbell.Domain;

/// <summary>
/// Marks an entity guarded by an <see cref="int"/> concurrency token.
/// </summary>
/// <remarks>
/// ADR 0001 rules out <c>rowversion</c> and <c>xmin</c> — each is native to exactly one of the two
/// providers. A plain integer is native to neither, so it behaves identically on both. Nothing
/// increments it automatically, so the DbContext does it on save.
/// </remarks>
public interface IConcurrencyTracked
{
    int ConcurrencyToken { get; }
}
