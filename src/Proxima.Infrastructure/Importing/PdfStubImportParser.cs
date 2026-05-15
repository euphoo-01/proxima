using Proxima.Core.Application.Importing;
namespace Proxima.Infrastructure.Importing;

public sealed class PdfStubImportParser : IImportParser
{
    public ImportFileType SupportedFileType => ImportFileType.Pdf;

    public Task<ImportPreview> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ImportPreview(
            false,
            "PDF parser пока ограничен. Используйте ручной ввод.",
            [],
            true));
    }
}
