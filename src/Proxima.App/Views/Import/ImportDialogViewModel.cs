using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.ViewModels;
using Proxima.Importing;

namespace Proxima.App.Views.Import;

public sealed class ImportDialogViewModel : ViewModelBase
{
    private readonly IImportPreviewGateway _importPreviewGateway;
    private readonly AsyncCommand _parseFileCommand;
    private readonly DelegateCommand _openManualImportCommand;
    private readonly DelegateCommand _closeDialogCommand;
    private readonly DelegateCommand _clearSelectionCommand;
    private readonly DelegateCommand _markDragOverCommand;
    private readonly DelegateCommand _clearDragOverCommand;

    private bool _isOpen;
    private bool _isDragOver;
    private bool _isParsing;
    private string _filePath = string.Empty;
    private string _statusMessage = "Перетащите CSV/PDF или укажите путь к файлу.";
    private bool _parseFailed;
    private bool _hasValidationWarnings;

    public ImportDialogViewModel(IImportPreviewGateway importPreviewGateway, ManualImportViewModel manualImport)
    {
        _importPreviewGateway = importPreviewGateway;
        ManualImport = manualImport;
        WarningItems = [];

        _parseFileCommand = new AsyncCommand(ParseFileAsync, () => IsOpen && !IsParsing && !string.IsNullOrWhiteSpace(FilePath));
        _openManualImportCommand = new DelegateCommand(_ => OpenManualImport());
        _closeDialogCommand = new DelegateCommand(_ => Close());
        _clearSelectionCommand = new DelegateCommand(_ => ClearSelection());
        _markDragOverCommand = new DelegateCommand(_ => IsDragOver = true);
        _clearDragOverCommand = new DelegateCommand(_ => IsDragOver = false);
    }

    public event EventHandler? RequestClose;

    public event EventHandler? RequestOpenManualImport;

    public ManualImportViewModel ManualImport { get; }

    public ObservableCollection<string> WarningItems { get; }

    public ICommand ParseFileCommand => _parseFileCommand;

    public ICommand OpenManualImportCommand => _openManualImportCommand;

    public ICommand CloseDialogCommand => _closeDialogCommand;

    public ICommand ClearSelectionCommand => _clearSelectionCommand;

    public ICommand MarkDragOverCommand => _markDragOverCommand;

    public ICommand ClearDragOverCommand => _clearDragOverCommand;

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (SetProperty(ref _isOpen, value))
            {
                _parseFileCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CurrentState));
            }
        }
    }

    public bool IsDragOver
    {
        get => _isDragOver;
        private set
        {
            if (SetProperty(ref _isDragOver, value))
            {
                OnPropertyChanged(nameof(CurrentState));
            }
        }
    }

    public bool IsParsing
    {
        get => _isParsing;
        private set
        {
            if (SetProperty(ref _isParsing, value))
            {
                _parseFileCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CurrentState));
            }
        }
    }

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                _parseFileCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(HasFileSelected));
                OnPropertyChanged(nameof(CurrentState));
                OnPropertyChanged(nameof(SelectedFileType));
                OnPropertyChanged(nameof(IsCsvFile));
                OnPropertyChanged(nameof(IsPdfFile));
                OnPropertyChanged(nameof(IsUnknownFile));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool ParseFailed
    {
        get => _parseFailed;
        private set
        {
            if (SetProperty(ref _parseFailed, value))
            {
                OnPropertyChanged(nameof(CurrentState));
            }
        }
    }

    public bool HasValidationWarnings
    {
        get => _hasValidationWarnings;
        private set
        {
            if (SetProperty(ref _hasValidationWarnings, value))
            {
                OnPropertyChanged(nameof(CurrentState));
            }
        }
    }

    public bool HasFileSelected => !string.IsNullOrWhiteSpace(FilePath);

    public bool IsIdle => !IsDragOver && !HasFileSelected && !IsParsing && !ParseFailed;

    public string CurrentState => IsParsing
        ? "parsing"
        : ParseFailed
            ? "parse_failed"
            : HasValidationWarnings
                ? "validation_warnings"
                : IsDragOver
                    ? "drag_over"
                    : HasFileSelected
                        ? "file_selected"
                        : "idle";

    public string SelectedFileType => Path.GetExtension(FilePath).ToLowerInvariant() switch
    {
        ".pdf" => "PDF",
        ".csv" => "CSV",
        _ => "UNKNOWN"
    };

    public bool IsCsvFile => SelectedFileType == "CSV";

    public bool IsPdfFile => SelectedFileType == "PDF";

    public bool IsUnknownFile => SelectedFileType == "UNKNOWN";

    public void Open()
    {
        IsOpen = true;
        IsDragOver = false;
        ParseFailed = false;
        HasValidationWarnings = false;
        WarningItems.Clear();
        StatusMessage = "Перетащите CSV/PDF или укажите путь к файлу.";
    }

    public void Close()
    {
        IsOpen = false;
        IsDragOver = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void ClearSelection()
    {
        FilePath = string.Empty;
        IsDragOver = false;
        ParseFailed = false;
        HasValidationWarnings = false;
        WarningItems.Clear();
        StatusMessage = "Выбор файла очищен.";
    }

    private async Task ParseFileAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            return;
        }

        IsDragOver = false;
        IsParsing = true;
        ParseFailed = false;
        HasValidationWarnings = false;
        WarningItems.Clear();
        StatusMessage = "Проверяем и разбираем файл...";

        try
        {
            ImportPreview preview = await _importPreviewGateway.PreviewAsync(FilePath).ConfigureAwait(true);
            if (!preview.Succeeded)
            {
                ParseFailed = true;
                StatusMessage = "Не удалось разобрать файл. Попробуйте ручной импорт.";
                ManualImport.LoadFromPreview(preview);
                return;
            }

            ManualImport.LoadFromPreview(preview);

            IReadOnlyList<ImportedTransactionRow> suspiciousRows = preview.Rows
                .Where(static row => row.Status != ImportRowStatus.Valid)
                .ToArray();

            if (suspiciousRows.Count > 0)
            {
                HasValidationWarnings = true;
                WarningItems.Clear();
                foreach (ImportedTransactionRow row in suspiciousRows.Take(5))
                {
                    WarningItems.Add($"Строка {row.RowNumber}: {row.StatusReason ?? "Требует проверки"}");
                }

                if (suspiciousRows.Count > 5)
                {
                    WarningItems.Add($"И ещё {suspiciousRows.Count - 5} строк(и) требуют проверки.");
                }

                StatusMessage = "Есть подозрительные строки. Проверьте их в ручном импорте.";
                return;
            }

            StatusMessage = $"Файл разобран: {preview.Rows.Count} строк готовы к импорту.";
        }
        catch
        {
            ParseFailed = true;
            StatusMessage = "Ошибка импорта. Показываем только обезличенную информацию.";
        }
        finally
        {
            IsParsing = false;
        }
    }

    private void OpenManualImport()
    {
        RequestOpenManualImport?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
    {
        private readonly Func<Task> _execute = execute;
        private readonly Func<bool> _canExecute = canExecute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute();

        public async void Execute(object? parameter)
        {
            await _execute().ConfigureAwait(true);
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public interface IImportPreviewGateway
{
    Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default);
}

public sealed class ImportPreviewGateway(IImportService importService) : IImportPreviewGateway
{
    private readonly IImportService _importService = importService;

    public Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return _importService.PreviewAsync(filePath, cancellationToken);
    }
}
