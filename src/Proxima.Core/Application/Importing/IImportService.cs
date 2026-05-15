namespace Proxima.Core.Application.Importing;

public interface IImportService
{
    Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default);
}
