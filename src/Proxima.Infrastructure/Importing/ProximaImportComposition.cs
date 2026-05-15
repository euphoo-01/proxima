using Proxima.Core.Application.Importing;
namespace Proxima.Infrastructure.Importing;

public static class ProximaImportComposition
{
    public static IImportService CreateImportService(long? maxBytes = null)
    {
        return new ImportService(
            new ImportFileValidator(maxBytes ?? ImportFileValidator.DefaultMaxBytes),
            [new CsvImportParser(), new PdfStubImportParser()]);
    }
}
