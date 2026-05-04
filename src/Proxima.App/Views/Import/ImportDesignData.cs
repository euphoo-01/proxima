namespace Proxima.App.Views.Import;

public static class ImportDesignData
{
    public static ManualImportViewModel ManualImportViewModel { get; } = new();

    public static ImportDialogViewModel ImportDialog { get; } = CreateImportDialog();

    private static ImportDialogViewModel CreateImportDialog()
    {
        Proxima.App.Navigation.AppNavigationService navigation = new();
        navigation.Register(new Proxima.App.Navigation.AppRoute(Proxima.App.Navigation.AppRoutes.ImportPreview, "Импорт активов", "Все активы / Импорт активов"));
        navigation.Register(new Proxima.App.Navigation.AppRoute(Proxima.App.Navigation.AppRoutes.ManualImport, "Ручной импорт", "Все активы / Ручной импорт"));
        Proxima.Importing.IImportService importService = new Proxima.Importing.ImportService(
            new Proxima.Importing.ImportFileValidator(Proxima.Importing.ImportFileValidator.DefaultMaxBytes),
            [new Proxima.Importing.CsvImportParser(), new Proxima.Importing.PdfStubImportParser()]);

        return new ImportDialogViewModel(navigation, new ImportPreviewGateway(importService), ManualImportViewModel);
    }
}
