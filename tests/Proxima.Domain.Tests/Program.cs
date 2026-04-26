using Proxima.Domain;

namespace Proxima.Domain.Tests;

internal static class Program
{
    private static void Main()
    {
        AssemblyName_IsProximaDomain();
        Domain_HasNoForbiddenDependencies();
        Console.WriteLine("Proxima.Domain.Tests passed.");
    }

    private static void AssemblyName_IsProximaDomain()
    {
        string? assemblyName = typeof(DomainAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Domain", "Domain assembly name must be Proxima.Domain.");
    }

    private static void Domain_HasNoForbiddenDependencies()
    {
        string[] forbidden =
        [
            "Avalonia",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "System.Net.Http",
        ];

        string[] references = typeof(DomainAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        foreach (string forbiddenReference in forbidden)
        {
            bool hasForbiddenReference = references.Any(reference =>
                reference.StartsWith(forbiddenReference, StringComparison.Ordinal));

            Assert(!hasForbiddenReference, $"Domain must not reference {forbiddenReference}.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
