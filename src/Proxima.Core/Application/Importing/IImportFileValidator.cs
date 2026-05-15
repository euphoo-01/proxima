namespace Proxima.Core.Application.Importing;

public interface IImportFileValidator
{
    ImportFileValidationResult Validate(string filePath, long fileSizeBytes);
}
