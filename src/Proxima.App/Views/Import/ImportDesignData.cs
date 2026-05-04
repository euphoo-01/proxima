namespace Proxima.App.Views.Import;

public static class ImportDesignData
{
    public static ManualImportViewModel ManualImportViewModel { get; } = new();

    public static ImportDialogViewModel ImportDialog { get; } = CreateImportDialog();

    private static ImportDialogViewModel CreateImportDialog()
    {
        Proxima.Importing.IImportService importService = new Proxima.Importing.ImportService(
            new Proxima.Importing.ImportFileValidator(Proxima.Importing.ImportFileValidator.DefaultMaxBytes),
            [new Proxima.Importing.CsvImportParser(), new Proxima.Importing.PdfStubImportParser()]);

        return new ImportDialogViewModel(new ImportPreviewGateway(importService), ManualImportViewModel);
    }
}
