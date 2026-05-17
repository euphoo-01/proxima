using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Proxima.App.Views.Import;

public partial class ManualImportView : UserControl
{
    public ManualImportView()
    {
        InitializeComponent();
    }

    private async void ChooseFileButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите CSV-файл с транзакциями",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CSV отчёт")
                {
                    Patterns = ["*.csv"],
                    MimeTypes = ["text/csv", "application/csv", "text/plain"],
                },
            ],
        }).ConfigureAwait(true);

        string? localPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath) && DataContext is ManualImportViewModel viewModel)
        {
            viewModel.SetSelectedFileAndParse(localPath);
        }
    }
}
