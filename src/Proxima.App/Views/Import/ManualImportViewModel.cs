using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Proxima.App.Shell;
using Proxima.Core.Application.Transactions;
using Proxima.Core.Application.MarketData;
using Proxima.App.ViewModels;
using Proxima.App.Notifications;
using Proxima.Core.Domain.Transactions;
using Proxima.Core.Application.Importing;

namespace Proxima.App.Views.Import;

public sealed class ManualImportViewModel : ViewModelBase
{
    private const long MaxCsvFileSizeBytes = 5 * 1024 * 1024;

    private readonly IImportCommitService? _importCommitService;
    private readonly IShellState? _shellState;
    private readonly IRuntimeDataInvalidation? _runtimeDataInvalidation;
    private readonly IImportPreviewGateway? _importPreviewGateway;
    private readonly IMarketSymbolSearchService? _symbolSearchService;
    private readonly IAppNotificationCenter? _notificationCenter;

    private readonly DelegateCommand _addRowCommand;
    private readonly DelegateCommand _removeRowCommand;
    private readonly DelegateCommand _clearRowsCommand;
    private readonly DelegateCommand _selectBuyCommand;
    private readonly DelegateCommand _selectSellCommand;
    private readonly AsyncCommand _addManualTransactionCommand;
    private readonly DelegateCommand _selectNewSymbolCommand;
    private readonly DelegateCommand _clearFileCommand;
    private readonly AsyncCommand _parseFileCommand;
    private readonly AsyncCommand _saveCommand;

    private string _statusMessage = "Перетащите CSV-файл или добавьте транзакцию вручную.";
    private string _errorMessage = string.Empty;
    private bool _hasWarnings;
    private bool _isParsing;
    private string _filePath = string.Empty;

    private string _newAssetName = string.Empty;
    private string _newSymbolSearchMessage = string.Empty;
    private bool _isApplyingNewSymbolSelection;
    private CancellationTokenSource? _newSymbolSearchCts;
    private MarketSymbolCandidate? _selectedNewSymbol;
    private string _newTag = "Акции";
    private TransactionType _newTransactionType = TransactionType.Buy;
    private string _newPrice = "0.00";
    private string _newQuantity = "1";
    private string _newDate = DateTimeOffset.Now.ToString("dd.MM.yy", CultureInfo.CurrentCulture);

    public ManualImportViewModel()
        : this(null, null, null, null, null, null)
    {
    }

    public ManualImportViewModel(
        IImportCommitService? importCommitService,
        IShellState? shellState,
        IRuntimeDataInvalidation? runtimeDataInvalidation,
        IImportPreviewGateway? importPreviewGateway,
        IMarketSymbolSearchService? symbolSearchService = null,
        IAppNotificationCenter? notificationCenter = null)
    {
        _importCommitService = importCommitService;
        _shellState = shellState;
        _runtimeDataInvalidation = runtimeDataInvalidation;
        _importPreviewGateway = importPreviewGateway;
        _symbolSearchService = symbolSearchService;
        _notificationCenter = notificationCenter;

        Rows = [];
        NewSymbolSuggestions = [];
        OperationTypeLabels = ["Купить", "Продать"];

        _addRowCommand = new DelegateCommand(_ => AddEmptyRow());
        _removeRowCommand = new DelegateCommand(row => RemoveRow(row as ManualTransactionRowViewModel));
        _clearRowsCommand = new DelegateCommand(_ => ClearRows());
        _selectBuyCommand = new DelegateCommand(_ => NewTransactionType = TransactionType.Buy);
        _selectSellCommand = new DelegateCommand(_ => NewTransactionType = TransactionType.Sell);
        _addManualTransactionCommand = new AsyncCommand(AddManualTransactionAsync, () => !IsParsing);
        _selectNewSymbolCommand = new DelegateCommand(SelectNewSymbol);
        _clearFileCommand = new DelegateCommand(_ => ClearFile());
        _parseFileCommand = new AsyncCommand(ParseSelectedFileAsync, () => !IsParsing && HasFileSelected);
        _saveCommand = new AsyncCommand(SaveImportAsync, () => !IsParsing && Rows.Count > 0);
    }

    public ObservableCollection<ManualTransactionRowViewModel> Rows { get; }

    public ObservableCollection<MarketSymbolCandidate> NewSymbolSuggestions { get; }

    public IReadOnlyList<string> OperationTypeLabels { get; }

    public ICommand AddRowCommand => _addRowCommand;

    public ICommand RemoveRowCommand => _removeRowCommand;

    public ICommand ClearRowsCommand => _clearRowsCommand;

    public ICommand SaveCommand => _saveCommand;

    public ICommand SelectBuyCommand => _selectBuyCommand;

    public ICommand SelectSellCommand => _selectSellCommand;

    public ICommand AddManualTransactionCommand => _addManualTransactionCommand;

    public ICommand SelectNewSymbolCommand => _selectNewSymbolCommand;

    public ICommand ParseFileCommand => _parseFileCommand;

    public ICommand ClearFileCommand => _clearFileCommand;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasWarnings
    {
        get => _hasWarnings;
        private set => SetProperty(ref _hasWarnings, value);
    }

    public bool IsParsing
    {
        get => _isParsing;
        private set
        {
            if (SetProperty(ref _isParsing, value))
            {
                _parseFileCommand.RaiseCanExecuteChanged();
                _saveCommand.RaiseCanExecuteChanged();
                _addManualTransactionCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(ImportButtonText));
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
                OnPropertyChanged(nameof(SelectedFileName));
                OnPropertyChanged(nameof(SelectedFileBadge));
            }
        }
    }

    public bool HasFileSelected => !string.IsNullOrWhiteSpace(FilePath);

    public string SelectedFileName => HasFileSelected ? Path.GetFileName(FilePath) : "Файл не выбран";

    public string SelectedFileBadge => HasFileSelected ? "CSV выбран" : "CSV до 5 МБ";

    public string ImportButtonText => IsParsing ? "Проверяем..." : "Проверить файл";

    public string NewAssetName
    {
        get => _newAssetName;
        set
        {
            if (SetProperty(ref _newAssetName, value))
            {
                if (!_isApplyingNewSymbolSelection)
                {
                    if (_selectedNewSymbol is not null
                        && !string.Equals(value.Trim(), _selectedNewSymbol.Symbol, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value.Trim(), _selectedNewSymbol.DisplaySymbol, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedNewSymbol = null;
                    }

                    _ = SearchNewSymbolsAsync(value);
                }
            }
        }
    }

    public string NewSymbolSearchMessage
    {
        get => _newSymbolSearchMessage;
        private set
        {
            if (SetProperty(ref _newSymbolSearchMessage, value))
            {
                OnPropertyChanged(nameof(HasNewSymbolSearchMessage));
            }
        }
    }

    public bool HasNewSymbolSearchMessage => !string.IsNullOrWhiteSpace(NewSymbolSearchMessage);

    public bool HasNewSymbolSuggestions => NewSymbolSuggestions.Count > 0;

    public string NewTag
    {
        get => _newTag;
        set => SetProperty(ref _newTag, value);
    }

    public TransactionType NewTransactionType
    {
        get => _newTransactionType;
        set
        {
            if (SetProperty(ref _newTransactionType, value))
            {
                OnPropertyChanged(nameof(IsNewBuy));
                OnPropertyChanged(nameof(IsNewSell));
            }
        }
    }

    public bool IsNewBuy => NewTransactionType == TransactionType.Buy;

    public bool IsNewSell => NewTransactionType == TransactionType.Sell;

    public string NewPrice
    {
        get => _newPrice;
        set => SetProperty(ref _newPrice, value);
    }

    public string NewQuantity
    {
        get => _newQuantity;
        set => SetProperty(ref _newQuantity, value);
    }

    public string NewDate
    {
        get => _newDate;
        set => SetProperty(ref _newDate, value);
    }

    public int SuspiciousCount => Rows.Count(static row => row.IsSuspicious);

    public int TotalCount => Rows.Count;

    public int ValidCount => Rows.Count - SuspiciousCount;

    public bool HasRows => Rows.Count > 0;

    public bool HasNoRows => Rows.Count == 0;

    public string TotalCountLabel => $"Всего: {TotalCount}";

    public string ValidCountLabel => $"Готово: {ValidCount}";

    public string SuspiciousCountLabel => $"Проверить: {SuspiciousCount}";

    public void SetSelectedFileAndParse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        FilePath = filePath;
        _parseFileCommand.Execute(null);
    }

    public void LoadFromPreview(ImportPreview preview)
    {
        ClearRowsInternal();
        foreach (ImportedTransactionRow row in preview.Rows)
        {
            ManualTransactionRowViewModel viewModel = new()
            {
                Date = row.TradeDate.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture),
                TickerOrName = string.IsNullOrWhiteSpace(row.AssetTicker) ? row.AssetName : row.AssetTicker,
                OperationType = row.TransactionType.ToString(),
                Quantity = row.Quantity.ToString("0.####", CultureInfo.InvariantCulture),
                Price = row.Price.ToString("0.####", CultureInfo.InvariantCulture),
                Commission = row.FeeAmount.ToString("0.####", CultureInfo.InvariantCulture),
                Currency = string.IsNullOrWhiteSpace(row.Currency) ? "USD" : row.Currency,
                TagOrCategory = row.Tag ?? string.Empty,
                IsSuspicious = row.Status != ImportRowStatus.Valid,
                SuspiciousReason = row.StatusReason ?? string.Empty,
            };

            AttachRow(viewModel);
            Rows.Add(viewModel);
        }

        RecalculateWarnings();
        StatusMessage = preview.Succeeded
            ? $"Загружено строк: {Rows.Count}. Проверьте таблицу и нажмите «Сохранить»."
            : "Автоматический импорт не удался. Исправьте данные вручную.";
    }

    private async Task ParseSelectedFileAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            ShowError("Выберите CSV-файл для импорта.");
            return;
        }

        IsParsing = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Проверяем размер, расширение и структуру CSV-файла...";

        try
        {
            string normalizedPath = ExpandLocalPath(FilePath);
            if (!string.Equals(FilePath, normalizedPath, StringComparison.Ordinal))
            {
                FilePath = normalizedPath;
            }

            FileInfo file = new(normalizedPath);
            if (!file.Exists)
            {
                ShowError("Файл не найден. Проверьте путь или выберите файл заново.");
                return;
            }

            if (file.Length > MaxCsvFileSizeBytes)
            {
                ShowError("Ошибка: файл больше 5 МБ. Загрузите CSV меньшего размера.");
                return;
            }

            if (!string.Equals(file.Extension, ".csv", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Ошибка: поддерживаются только CSV-файлы.");
                return;
            }

            if (_importPreviewGateway is null)
            {
                ShowError("Модуль импорта не подключен. Проверьте регистрацию IImportPreviewGateway.");
                return;
            }

            ImportPreview preview = await _importPreviewGateway.PreviewAsync(FilePath).ConfigureAwait(true);
            if (!preview.Succeeded)
            {
                ShowError(string.IsNullOrWhiteSpace(preview.Message)
                    ? "Ошибка: структура CSV не соответствует ожидаемому формату."
                    : preview.Message);
                return;
            }

            if (preview.Rows.Count == 0)
            {
                ShowError("Ошибка: CSV не содержит транзакций.");
                return;
            }

            bool hasStructureError = preview.Rows.Any(row =>
                row.Status == ImportRowStatus.Invalid
                && (row.StatusReason?.Contains("Недостаточно колонок", StringComparison.OrdinalIgnoreCase) ?? false));
            bool allRowsInvalid = preview.Rows.All(static row => row.Status == ImportRowStatus.Invalid);
            if (hasStructureError || allRowsInvalid)
            {
                ShowError("Ошибка: структура CSV не соответствует ожидаемому формату. Ожидаемые поля: дата, тикер, название, тип, количество, цена, валюта, комиссия, брокер, тег.");
                return;
            }

            LoadFromPreview(preview);
            ErrorMessage = string.Empty;
            StatusMessage = preview.Rows.Any(static row => row.Status != ImportRowStatus.Valid)
                ? "CSV разобран. Некоторые строки требуют ручной проверки перед сохранением."
                : $"CSV разобран: {preview.Rows.Count} транзакций готовы к сохранению.";
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка импорта: {ex.Message}");
        }
        finally
        {
            IsParsing = false;
        }
    }

    private async Task AddManualTransactionAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(NewAssetName))
        {
            ShowError("Укажите название актива или тикер.");
            return;
        }

        MarketSymbolCandidate? symbol = await ResolveNewSymbolAsync(NewAssetName, CancellationToken.None).ConfigureAwait(true);
        if (symbol is null)
        {
            return;
        }

        if (!TryParseDate(NewDate, out DateTimeOffset tradeDate))
        {
            ShowError("Проверьте дату. Поддерживаются форматы ДД.ММ.ГГ, ДД/ММ/ГГ и YYYY-MM-DD.");
            return;
        }

        if (!TryParseDecimal(NewPrice, out decimal price) || price <= 0m)
        {
            ShowError("Цена должна быть числом больше нуля.");
            return;
        }

        if (!TryParseDecimal(NewQuantity, out decimal quantity) || quantity <= 0m)
        {
            ShowError("Количество должно быть числом больше нуля.");
            return;
        }

        ManualTransactionRowViewModel row = new()
        {
            Date = tradeDate.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture),
            TickerOrName = symbol.Symbol,
            OperationType = NewTransactionType.ToString(),
            Quantity = quantity.ToString("0.####", CultureInfo.InvariantCulture),
            Price = price.ToString("0.####", CultureInfo.InvariantCulture),
            Commission = "0",
            Currency = symbol.AssetType is Proxima.Core.Domain.Assets.AssetType.Cash
                ? "USD"
                : string.IsNullOrWhiteSpace(symbol.Currency) ? "USD" : symbol.Currency,
            TagOrCategory = string.IsNullOrWhiteSpace(NewTag) ? DefaultTagFor(symbol) : NewTag.Trim(),
        };

        AttachRow(row);
        Rows.Add(row);
        RecalculateWarnings();
        RefreshCollectionState();
        StatusMessage = $"Транзакция по {symbol.PrimaryText} добавлена в таблицу. Проверьте строку и сохраните импорт.";

        _selectedNewSymbol = null;
        NewSymbolSuggestions.Clear();
        OnPropertyChanged(nameof(HasNewSymbolSuggestions));
        NewAssetName = string.Empty;
        NewSymbolSearchMessage = string.Empty;
        NewPrice = "0.00";
        NewQuantity = "1";
        NewDate = DateTimeOffset.Now.ToString("dd.MM.yy", CultureInfo.CurrentCulture);
    }

    private async Task SearchNewSymbolsAsync(string query)
    {
        NewSymbolSearchMessage = string.Empty;
        _newSymbolSearchCts?.Cancel();

        if (_symbolSearchService is null || string.IsNullOrWhiteSpace(query) || query.Trim().Length == 0)
        {
            NewSymbolSuggestions.Clear();
            OnPropertyChanged(nameof(HasNewSymbolSuggestions));
            return;
        }

        CancellationTokenSource cts = new();
        _newSymbolSearchCts = cts;

        try
        {
            await Task.Delay(250, cts.Token).ConfigureAwait(true);
            MarketSymbolSearchResult result = await _symbolSearchService.SearchAsync(query, 8, cts.Token).ConfigureAwait(true);
            if (_newSymbolSearchCts != cts)
            {
                return;
            }

            NewSymbolSuggestions.Clear();
            foreach (MarketSymbolCandidate item in result.Symbols)
            {
                NewSymbolSuggestions.Add(item);
            }

            NewSymbolSearchMessage = result.Succeeded
                ? result.Symbols.Count == 0 ? "Twelve Data не нашёл инструмент по этому запросу." : string.Empty
                : result.Message;
            OnPropertyChanged(nameof(HasNewSymbolSuggestions));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SelectNewSymbol(object? parameter)
    {
        if (parameter is not MarketSymbolCandidate symbol)
        {
            return;
        }

        _selectedNewSymbol = symbol;
        _isApplyingNewSymbolSelection = true;
        NewAssetName = symbol.Symbol;
        _isApplyingNewSymbolSelection = false;
        if (string.IsNullOrWhiteSpace(NewTag))
        {
            NewTag = DefaultTagFor(symbol);
        }

        NewSymbolSearchMessage = string.Empty;
        NewSymbolSuggestions.Clear();
        OnPropertyChanged(nameof(HasNewSymbolSuggestions));
    }

    private async Task<MarketSymbolCandidate?> ResolveNewSymbolAsync(string query, CancellationToken cancellationToken)
    {
        if (_selectedNewSymbol is not null
            && (string.Equals(query.Trim(), _selectedNewSymbol.Symbol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(query.Trim(), _selectedNewSymbol.DisplaySymbol, StringComparison.OrdinalIgnoreCase)))
        {
            return _selectedNewSymbol;
        }

        if (_symbolSearchService is null)
        {
            return new MarketSymbolCandidate(query.Trim().ToUpperInvariant(), query.Trim().ToUpperInvariant(), query.Trim(), "Manual", "USD", string.Empty, null, Proxima.Core.Domain.Assets.AssetType.Stock);
        }

        MarketSymbolSearchResult result = await _symbolSearchService.ResolveAsync(query, cancellationToken).ConfigureAwait(true);
        if (!result.Succeeded || result.Symbols.Count == 0)
        {
            ShowError(string.IsNullOrWhiteSpace(result.Message)
                ? "Twelve Data не нашёл инструмент. Выберите тикер из подсказок."
                : result.Message);
            return null;
        }

        return result.Symbols[0];
    }

    private async Task<string?> ResolveImportSymbolAsync(string query, CancellationToken cancellationToken)
    {
        string value = query.Trim();
        if (_symbolSearchService is null)
        {
            return value.ToUpperInvariant();
        }

        MarketSymbolSearchResult result = await _symbolSearchService.ResolveAsync(value, cancellationToken).ConfigureAwait(true);
        if (!result.Succeeded || result.Symbols.Count == 0)
        {
            ShowError($"Twelve Data не подтвердил тикер «{value}». Исправьте строку или выберите инструмент через подсказки. {result.Message}".Trim());
            return null;
        }

        return result.Symbols[0].Symbol;
    }

    private static string DefaultTagFor(MarketSymbolCandidate symbol)
    {
        return symbol.AssetType switch
        {
            Proxima.Core.Domain.Assets.AssetType.Crypto => "Криптовалюта",
            Proxima.Core.Domain.Assets.AssetType.Etf => "ETF",
            Proxima.Core.Domain.Assets.AssetType.Bond => "Облигации",
            Proxima.Core.Domain.Assets.AssetType.Currency => "Валюта",
            Proxima.Core.Domain.Assets.AssetType.Cash => "Наличность",
            _ => "Акции",
        };
    }

    private void AddEmptyRow()
    {
        ManualTransactionRowViewModel row = new()
        {
            Date = DateTimeOffset.Now.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture),
            OperationType = TransactionType.Buy.ToString(),
            Currency = "USD",
            Quantity = "1",
            Price = "0",
            TagOrCategory = "Акции",
        };

        AttachRow(row);
        Rows.Add(row);
        RecalculateWarnings();
        RefreshCollectionState();
        StatusMessage = "Добавлена пустая строка. Заполните обязательные поля.";
    }

    private void RemoveRow(ManualTransactionRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        DetachRow(row);
        Rows.Remove(row);
        RecalculateWarnings();
        RefreshCollectionState();
        StatusMessage = Rows.Count == 0
            ? "Все строки удалены. Добавьте транзакцию вручную или загрузите CSV."
            : "Транзакция удалена из таблицы.";
    }

    private void ClearRows()
    {
        ClearRowsInternal();
        RecalculateWarnings();
        RefreshCollectionState();
        StatusMessage = "Таблица очищена.";
    }

    private void ClearRowsInternal()
    {
        foreach (ManualTransactionRowViewModel item in Rows)
        {
            DetachRow(item);
        }

        Rows.Clear();
    }

    private void ClearFile()
    {
        FilePath = string.Empty;
        ErrorMessage = string.Empty;
        StatusMessage = "Файл очищен. Перетащите новый CSV или добавьте транзакцию вручную.";
    }

    private async Task SaveImportAsync()
    {
        ErrorMessage = string.Empty;
        RecalculateWarnings();

        if (Rows.Count == 0)
        {
            ShowError("Нет транзакций для сохранения.");
            return;
        }

        if (HasWarnings)
        {
            ShowError("Исправьте строки с ошибками перед сохранением.");
            return;
        }

        if (_importCommitService is null || _shellState is null)
        {
            StatusMessage = "Импорт сохранён в демо-режиме.";
            _ = _notificationCenter?.NotifyAsync(AppNotificationLevel.Success, "Импорт сохранен", StatusMessage, "Ручной импорт");
            return;
        }

        List<ImportTransactionDraft> drafts = [];
        foreach (ManualTransactionRowViewModel row in Rows)
        {
            if (!TryParseDate(row.Date, out DateTimeOffset tradeDate)
                || !Enum.TryParse(row.OperationType, true, out TransactionType type)
                || !TryParseDecimal(row.Quantity, out decimal quantity)
                || !TryParseDecimal(row.Price, out decimal price)
                || !TryParseDecimal(string.IsNullOrWhiteSpace(row.Commission) ? "0" : row.Commission, out decimal commission)
                || string.IsNullOrWhiteSpace(row.Currency)
                || string.IsNullOrWhiteSpace(row.TickerOrName))
            {
                ShowError("Импорт не выполнен: проверьте формат дат, чисел и тикеров.");
                return;
            }

            string? resolvedSymbol = await ResolveImportSymbolAsync(row.TickerOrName, CancellationToken.None).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(resolvedSymbol))
            {
                return;
            }

            drafts.Add(new ImportTransactionDraft(
                NormalizeTradeDateForStorage(tradeDate),
                resolvedSymbol,
                type,
                quantity,
                price,
                commission,
                row.Currency.Trim().ToUpperInvariant(),
                Notes: null,
                Tag: string.IsNullOrWhiteSpace(row.TagOrCategory) ? null : row.TagOrCategory.Trim()));
        }

        ImportCommitResult result = await _importCommitService
            .CommitAsync(_shellState.CurrentPortfolioId, drafts, CancellationToken.None)
            .ConfigureAwait(true);

        StatusMessage = result.Message;
        if (!result.Succeeded)
        {
            ShowError(result.Message);
            return;
        }

        if (result.SavedRows > 0)
        {
            _runtimeDataInvalidation?.Invalidate("unified-import-commit");
            StatusMessage = $"Сохранено транзакций: {result.SavedRows}. Дашборд, список активов и карточки активов будут пересчитаны.";
            _ = _notificationCenter?.NotifyAsync(AppNotificationLevel.Success, "Импорт сохранен", StatusMessage, "Ручной импорт");
        }
    }

    private static string ExpandLocalPath(string path)
    {
        string trimmed = path.Trim().Trim('"');
        if (trimmed.StartsWith("~/", StringComparison.Ordinal) || trimmed.Equals("~", StringComparison.Ordinal))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return trimmed.Length == 1 ? home : Path.Combine(home, trimmed[2..]);
        }

        if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return trimmed;
    }

    private static DateTimeOffset NormalizeTradeDateForStorage(DateTimeOffset value)
    {
        if (value.TimeOfDay == TimeSpan.Zero)
        {
            return new DateTimeOffset(value.Year, value.Month, value.Day, 12, 0, 0, TimeSpan.Zero);
        }

        return value.ToUniversalTime();
    }

    private void AttachRow(ManualTransactionRowViewModel row)
    {
        row.PropertyChanged += HandleRowPropertyChanged;
        row.RecalculateSuspicion();
    }

    private void DetachRow(ManualTransactionRowViewModel row)
    {
        row.PropertyChanged -= HandleRowPropertyChanged;
    }

    private void HandleRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ManualTransactionRowViewModel row)
        {
            row.RecalculateSuspicion();
        }

        RefreshSummaryProperties();
    }

    private void RecalculateWarnings()
    {
        foreach (ManualTransactionRowViewModel row in Rows)
        {
            row.RecalculateSuspicion();
        }

        RefreshSummaryProperties();
    }

    private void RefreshSummaryProperties()
    {
        HasWarnings = Rows.Any(static row => row.IsSuspicious);
        OnPropertyChanged(nameof(SuspiciousCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ValidCount));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(TotalCountLabel));
        OnPropertyChanged(nameof(ValidCountLabel));
        OnPropertyChanged(nameof(SuspiciousCountLabel));
        _saveCommand.RaiseCanExecuteChanged();
    }

    private void RefreshCollectionState()
    {
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(TotalCountLabel));
        OnPropertyChanged(nameof(ValidCountLabel));
        OnPropertyChanged(nameof(SuspiciousCountLabel));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ValidCount));
        OnPropertyChanged(nameof(SuspiciousCount));
        _saveCommand.RaiseCanExecuteChanged();
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        StatusMessage = message;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _ = _notificationCenter?.NotifyAsync(AppNotificationLevel.Error, "Ошибка импорта", message, "Ручной импорт");
        }
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        string normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryParseDate(string value, out DateTimeOffset result)
    {
        string compact = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        string[] formats =
        [
            "yyyy-MM-dd",
            "dd.MM.yyyy",
            "dd.MM.yy",
            "dd/MM/yyyy",
            "dd/MM/yy",
            "dd-MM-yyyy",
            "dd-MM-yy",
        ];

        if (DateTimeOffset.TryParseExact(compact, formats, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out result))
        {
            return true;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out result)
            || DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result);
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

public sealed class ManualTransactionRowViewModel : ViewModelBase
{
    private string _date = string.Empty;
    private string _tickerOrName = string.Empty;
    private string _operationType = TransactionType.Buy.ToString();
    private string _quantity = string.Empty;
    private string _price = string.Empty;
    private string _commission = "0";
    private string _currency = "USD";
    private string _tagOrCategory = string.Empty;
    private bool _isSuspicious;
    private string _suspiciousReason = string.Empty;

    public string Date
    {
        get => _date;
        set
        {
            if (SetProperty(ref _date, value))
            {
                OnPropertyChanged(nameof(DateDisplay));
            }
        }
    }

    public string DateDisplay => Date;

    public string TickerOrName
    {
        get => _tickerOrName;
        set => SetProperty(ref _tickerOrName, value);
    }

    public string OperationType
    {
        get => _operationType;
        set
        {
            if (SetProperty(ref _operationType, value))
            {
                OnPropertyChanged(nameof(OperationTypeLabel));
                OnPropertyChanged(nameof(TypePillText));
                OnPropertyChanged(nameof(IsBuy));
                OnPropertyChanged(nameof(IsSell));
                RaiseAmountProperties();
            }
        }
    }

    public string OperationTypeLabel
    {
        get => OperationType switch
        {
            nameof(TransactionType.Sell) => "Продать",
            _ => "Купить",
        };
        set => OperationType = value switch
        {
            "Продать" => TransactionType.Sell.ToString(),
            "Sell" => TransactionType.Sell.ToString(),
            _ => TransactionType.Buy.ToString(),
        };
    }

    public string TypePillText => IsSell ? "Ордер продажи" : "Ордер покупки";

    public bool IsBuy => !IsSell;

    public bool IsSell => string.Equals(OperationType, TransactionType.Sell.ToString(), StringComparison.OrdinalIgnoreCase);

    public string Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                RaiseAmountProperties();
            }
        }
    }

    public string Price
    {
        get => _price;
        set
        {
            if (SetProperty(ref _price, value))
            {
                RaiseAmountProperties();
            }
        }
    }

    public string Commission
    {
        get => _commission;
        set => SetProperty(ref _commission, value);
    }

    public string Currency
    {
        get => _currency;
        set
        {
            if (SetProperty(ref _currency, value))
            {
                RaiseAmountProperties();
            }
        }
    }

    public string TagOrCategory
    {
        get => _tagOrCategory;
        set => SetProperty(ref _tagOrCategory, value);
    }

    public bool IsSuspicious
    {
        get => _isSuspicious;
        set => SetProperty(ref _isSuspicious, value);
    }

    public string SuspiciousReason
    {
        get => _suspiciousReason;
        set => SetProperty(ref _suspiciousReason, value);
    }

    public bool IsPositiveAmount => IsSell;

    public string SignedAmountDisplay
    {
        get
        {
            if (!TryParseDecimal(Quantity, out decimal qty) || !TryParseDecimal(Price, out decimal price))
            {
                return "—";
            }

            decimal amount = qty * price;
            string sign = IsSell ? "+" : "-";
            string currencySign = NormalizeCurrencySign(Currency);
            return $"{sign}{currencySign}{amount.ToString("N2", CultureInfo.InvariantCulture)}";
        }
    }

    public void RecalculateSuspicion()
    {
        bool invalidDate = !TryParseDate(Date, out _);
        bool invalidQty = !TryParseDecimal(Quantity, out decimal qty) || qty <= 0m;
        bool invalidPrice = !TryParseDecimal(Price, out decimal price) || price <= 0m;
        bool missingAsset = string.IsNullOrWhiteSpace(TickerOrName);
        bool missingCurrency = string.IsNullOrWhiteSpace(Currency);

        IsSuspicious = invalidDate || invalidQty || invalidPrice || missingAsset || missingCurrency;
        SuspiciousReason = IsSuspicious
            ? "Проверьте дату, актив, количество, цену и валюту. Эти поля обязательны для сохранения транзакции."
            : string.Empty;
    }

    private void RaiseAmountProperties()
    {
        OnPropertyChanged(nameof(SignedAmountDisplay));
        OnPropertyChanged(nameof(IsPositiveAmount));
    }

    private static string NormalizeCurrencySign(string currency)
    {
        return currency.Trim().ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "BYN" => "Br ",
            "RUB" => "₽",
            _ => string.IsNullOrWhiteSpace(currency) ? "$" : currency.Trim().ToUpperInvariant() + " ",
        };
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        string normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryParseDate(string value, out DateTimeOffset result)
    {
        string compact = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        string[] formats =
        [
            "yyyy-MM-dd",
            "dd.MM.yyyy",
            "dd.MM.yy",
            "dd/MM/yyyy",
            "dd/MM/yy",
            "dd-MM-yyyy",
            "dd-MM-yy",
        ];

        if (DateTimeOffset.TryParseExact(compact, formats, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out result))
        {
            return true;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out result)
            || DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result);
    }
}
