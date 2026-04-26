using Proxima.Importing;

namespace Proxima.Importing.Tests;

internal static class Program
{
    private static void Main()
    {
        string? assemblyName = typeof(ImportingAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Importing", "Importing assembly name must be Proxima.Importing.");
        Console.WriteLine("Proxima.Importing.Tests passed.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
