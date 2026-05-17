using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Importing;

namespace Proxima.Infrastructure.Importing;

public static class ImportComposition
{
    public static IServiceCollection AddImportModule(this IServiceCollection services)
    {
        services.AddSingleton<IImportService>(_ => CreateDefaultImportService());
        return services;
    }

    private static IImportService CreateDefaultImportService(long? maxBytes = null)
    {
        return new ImportService(
            new ImportFileValidator(maxBytes ?? ImportFileValidator.DefaultMaxBytes),
            [new CsvImportParser()]);
    }
}
