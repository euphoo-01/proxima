using Proxima.Analytics;

namespace Proxima.Analytics.Tests;

internal static class Program
{
    private static void Main()
    {
        string? assemblyName = typeof(AnalyticsAssemblyMarker).Assembly.GetName().Name;
        Assert(assemblyName == "Proxima.Analytics", "Analytics assembly name must be Proxima.Analytics.");
        Console.WriteLine("Proxima.Analytics.Tests passed.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
