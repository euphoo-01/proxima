using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace Proxima.App.Views.Import;

public partial class ManualImportView : UserControl
{
    public ManualImportView()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, HandleDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, HandleDragLeave);
        AddHandler(DragDrop.DropEvent, HandleDrop);
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

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            if (DataContext is ManualImportViewModel viewModel)
            {
                viewModel.IsDragOver = true;
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void HandleDragLeave(object? sender, DragEventArgs e)
    {
        if (DataContext is ManualImportViewModel viewModel)
        {
            viewModel.IsDragOver = false;
        }
    }

    private void HandleDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not ManualImportViewModel viewModel)
        {
            return;
        }

        viewModel.IsDragOver = false;
        IReadOnlyList<IStorageItem>? files = e.DataTransfer.TryGetFiles();
        string? localPath = files?
            .OfType<IStorageFile>()
            .Select(file => file.TryGetLocalPath())
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        if (!string.IsNullOrWhiteSpace(localPath))
        {
            viewModel.SetSelectedFileAndParse(localPath);
        }
    }
}
