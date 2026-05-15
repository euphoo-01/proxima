namespace Proxima.Core.Application.Importing;

public interface IImportParser
{
    ImportFileType SupportedFileType { get; }

    Task<ImportPreview> ParseAsync(string filePath, CancellationToken cancellationToken);
}
