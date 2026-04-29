namespace Proxima.Importing;

public interface IImportFileValidator
{
    ImportFileValidationResult Validate(string filePath, long fileSizeBytes);
}
