namespace Ironbell.Infrastructure;

/// <summary>
/// Which relational provider the context is configured against.
/// </summary>
/// <remarks>
/// Production is <see cref="SqlServer"/> (Azure SQL free offer). <see cref="Postgres"/> exists so
/// CI can run the whole slice suite against PostgreSQL from M0 — per ADR 0001 that dual-provider
/// matrix is the only thing that actually keeps the layer portable rather than nominally portable.
/// </remarks>
public enum DatabaseProvider
{
    SqlServer = 0,
    Postgres = 1,
}
