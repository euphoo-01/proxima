using Proxima.Infrastructure;

namespace Proxima.Infrastructure.Tests;

internal static class Program
{
    private static void Main()
    {
        string? assemblyName = typeof(InfrastructureAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Infrastructure", "Infrastructure assembly name must be Proxima.Infrastructure.");
        Console.WriteLine("Proxima.Infrastructure.Tests passed.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
