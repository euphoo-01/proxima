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
        Proxima.Core.Application.Importing.IImportService importService = new Proxima.Infrastructure.Importing.ImportService(
            new Proxima.Infrastructure.Importing.ImportFileValidator(Proxima.Infrastructure.Importing.ImportFileValidator.DefaultMaxBytes),
            [new Proxima.Infrastructure.Importing.CsvImportParser(), new Proxima.Infrastructure.Importing.PdfStubImportParser()]);

        return new ImportDialogViewModel(navigation, new ImportPreviewGateway(importService), ManualImportViewModel, new Proxima.App.Notifications.NoOpAppNotificationCenter());
    }
}
