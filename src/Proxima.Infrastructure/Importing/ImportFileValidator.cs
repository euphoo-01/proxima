using Proxima.Core.Application.Importing;
namespace Proxima.Infrastructure.Importing;

public sealed class ImportFileValidator(long maxBytes) : IImportFileValidator
{
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    public ImportFileValidationResult Validate(string filePath, long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new ImportFileValidationResult(false, "Путь к файлу не указан.", null);
        }

        if (fileSizeBytes <= 0)
        {
            return new ImportFileValidationResult(false, "Файл пустой.", null);
        }

        if (fileSizeBytes > maxBytes)
        {
            return new ImportFileValidationResult(false, $"Файл превышает лимит {maxBytes / 1024 / 1024} MB.", null);
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".csv" => new ImportFileValidationResult(true, string.Empty, ImportFileType.Csv),
            ".pdf" => new ImportFileValidationResult(true, string.Empty, ImportFileType.Pdf),
            _ => new ImportFileValidationResult(false, "Поддерживаются только .csv и .pdf.", null),
        };
    }
}
