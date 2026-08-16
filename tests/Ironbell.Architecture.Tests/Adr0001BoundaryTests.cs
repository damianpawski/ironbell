using System.Reflection;

namespace Ironbell.Architecture.Tests;

/// <summary>
/// ADR 0001's prohibitions, enforced at build time instead of remembered.
/// </summary>
/// <remarks>
/// The slice tests already run the data layer against both providers. These catch the rules that a
/// passing test suite would not: a prohibited dependency or an unportable type can sit in the code
/// for months and only bite on the day the provider changes, which is exactly when nobody wants a
/// surprise.
/// </remarks>
public class Adr0001BoundaryTests
{
    private static Assembly[] ProductionAssemblies =>
    [
        typeof(Domain.AssemblyMarker).Assembly,
        typeof(Infrastructure.AssemblyMarker).Assembly,
        typeof(Program).Assembly,
    ];

    /// <summary>Assemblies whose persisted shapes ADR 0001 governs.</summary>
    private static Assembly[] DataLayerAssemblies =>
    [
        typeof(Domain.AssemblyMarker).Assembly,
        typeof(Infrastructure.AssemblyMarker).Assembly,
    ];

    [Fact]
    public void Nothing_references_Dapper()
    {
        // Raw SQL is provider-specific and would pin the app to SQL Server, which is the one thing
        // the data layer is built not to be.
        var offenders = ProductionAssemblies
            .Where(assembly => assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name?.Contains("Dapper", StringComparison.OrdinalIgnoreCase) == true))
            .Select(assembly => assembly.GetName().Name)
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void No_persisted_type_exposes_a_DateTimeOffset()
    {
        // UTC DateTime only. The two providers round-trip DateTimeOffset differently, so allowing
        // it reintroduces the divergence the UTC converter exists to remove.
        var offenders = DataLayerAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(DateTimeOffsetMembers)
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void No_persisted_type_exposes_an_array()
    {
        // SQL Server has no array type and PostgreSQL does. Many-valued data is modelled as child
        // rows so the shape means the same thing on both.
        var offenders = DataLayerAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.Name.StartsWith('<'))
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(property => property.PropertyType.IsArray)
                .Select(property => $"{type.FullName}.{property.Name}"))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    private static IEnumerable<string> DateTimeOffsetMembers(Type type)
    {
        if (type.Name.StartsWith('<'))
        {
            yield break;
        }

        const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var property in type.GetProperties(Flags))
        {
            if (IsDateTimeOffset(property.PropertyType))
            {
                yield return $"{type.FullName}.{property.Name}";
            }
        }

        foreach (var field in type.GetFields(Flags).Where(field => !field.Name.Contains('<')))
        {
            if (IsDateTimeOffset(field.FieldType))
            {
                yield return $"{type.FullName}.{field.Name}";
            }
        }
    }

    private static bool IsDateTimeOffset(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) == typeof(DateTimeOffset);
}
