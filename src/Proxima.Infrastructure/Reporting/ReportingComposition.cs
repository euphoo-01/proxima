using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Reporting;

namespace Proxima.Infrastructure.Reporting;

public static class ReportingComposition
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddSingleton<IReportService>(_ => new SimplePdfReportService());
        return services;
    }
}

internal static class ReportPathDefaults
{
    public static string GetDefaultReportDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Proxima", "reports");
    }
}
