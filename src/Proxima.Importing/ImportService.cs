namespace Proxima.Importing;

public sealed class ImportService(
    IImportFileValidator fileValidator,
    IReadOnlyList<IImportParser> parsers) : IImportService
{
    public async Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default)
    {
        FileInfo info = new(filePath);
        if (!info.Exists)
        {
            return new ImportPreview(false, "Файл не найден.", [], false);
        }

        ImportFileValidationResult validation = fileValidator.Validate(filePath, info.Length);
        if (!validation.IsValid || validation.FileType is null)
        {
            return new ImportPreview(false, validation.Message, [], false);
        }

        IImportParser? parser = parsers.FirstOrDefault(item => item.SupportedFileType == validation.FileType.Value);
        if (parser is null)
        {
            return new ImportPreview(false, "Не найден parser для типа файла.", [], false);
        }

        try
        {
            return await parser.ParseAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return new ImportPreview(false, "Ошибка разбора файла. Проверьте формат и попробуйте ручной ввод.", [], validation.FileType == ImportFileType.Pdf);
        }
    }
}
