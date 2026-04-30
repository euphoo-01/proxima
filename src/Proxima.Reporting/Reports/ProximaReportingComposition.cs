namespace Proxima.Reporting.Reports;

public static class ProximaReportingComposition
{
    public static IReportService CreateReportService()
    {
        return new SimplePdfReportService();
    }

    public static string GetDefaultReportDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Proxima", "reports");
    }
}
