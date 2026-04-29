namespace Proxima.Importing;

public interface IImportService
{
    Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default);
}
