using Proxima.Application;

namespace Proxima.Application.Tests;

internal static class Program
{
    private static void Main()
    {
        string? assemblyName = typeof(ApplicationAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Application", "Application assembly name must be Proxima.Application.");
        Console.WriteLine("Proxima.Application.Tests passed.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
