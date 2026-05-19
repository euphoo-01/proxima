using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Threading;
using Proxima.App.Navigation;
using Proxima.App.Notifications;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.App.Auth;
using Proxima.Core.Application.Assets;
using Proxima.Core.Application.MarketData;
using Proxima.Core.Application.Quotes;
using Proxima.Core.Application.Reporting;
using Proxima.Core.Application.Taxes;
using Proxima.Core.Application.Transactions;
using Proxima.Core.Domain.Assets;
using Proxima.Core.Domain.Transactions;
using Proxima.App.Common.Commands;

namespace Proxima.App.Views.Assets;

public sealed class AssetsViewModel : ViewModelBase
{
    private const int CompactAssetLimit = 3;
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    private readonly IAppNavigationService _navigation;
    private readonly IAssetService _assetService;
    private readonly ITransactionService _transactionService;
    private readonly IQuoteRefreshService _quoteRefreshService;
    private readonly IShellState _shellState;
    private readonly IRuntimeDataInvalidation _dataInvalidation;
    private readonly IMarketSymbolSearchService _symbolSearchService;
    private readonly IAppNotificationCenter _notificationCenter;
    private readonly IReportService _reportService;
    private readonly ITaxService _taxService;
    private readonly IRuntimeUserContext _userContext;
    private readonly IExchangeRateProvider _exchangeRateProvider;
    private readonly AsyncCommand _loadCommand;
    private readonly AsyncCommand _addManualTransactionCommand;
    private readonly AsyncCommand _refreshQuotesCommand;
    private readonly AsyncCommand _exportReportCommand;
    private readonly DelegateCommand _openImportCommand;
    private readonly DelegateCommand _moreAssetsCommand;
    private readonly DelegateCommand _sortByNameCommand;
    private readonly DelegateCommand _sortByShareCommand;
    private readonly DelegateCommand _sortByValueCommand;
    private readonly DelegateCommand _sortByChangeCommand;
    private readonly DelegateCommand _selectManualBuyCommand;
    private readonly DelegateCommand _selectManualSellCommand;
    private readonly DelegateCommand _selectManualSymbolCommand;
    private IReadOnlyList<AssetListItemViewModel> _allAssets = [];
    private CancellationTokenSource? _manualSymbolSearchCts;
    private CancellationTokenSource? _taxMetricLoadCts;
    private MarketSymbolCandidate? _selectedManualSymbol;
    private IReadOnlyList<PortfolioTransaction> _transactions = [];
    private bool _isLoading;
    private bool _isBusy;
    private bool _hasError;
    private bool _isShowingAllAssets;
    private string _errorText = string.Empty;
    private string _searchQuery = string.Empty;
    private string _manualAssetName = string.Empty;
    private string _manualPriceText = string.Empty;
    private string _manualQuantityText = "1";
    private string _manualIsinText = string.Empty;
    private string _manualDateText = DateTimeOffset.Now.ToString("dd.MM.yyyy", RuCulture);
    private string _formMessage = string.Empty;
    private bool _hasFormError;
    private string _symbolSearchMessage = string.Empty;
    private bool _isApplyingManualSymbolSelection;
    private string _manualTagText = "Акции";
    private decimal _currentPortfolioProfitLoss;
    private ManualTransactionTypeOption _selectedManualTransactionType;
    private AssetSortMode _sortMode = AssetSortMode.Value;
    private bool _sortDescending = true;

    public AssetsViewModel(
        IAppNavigationService navigation,
        IAssetService assetService,
        ITransactionService transactionService,
        IQuoteRefreshService quoteRefreshService,
        IShellState shellState,
        IRuntimeDataInvalidation dataInvalidation,
        IMarketSymbolSearchService symbolSearchService,
        IAppNotificationCenter notificationCenter,
        IReportService reportService,
        ITaxService taxService,
        IRuntimeUserContext userContext,
        IExchangeRateProvider exchangeRateProvider)
    {
        _navigation = navigation;
        _assetService = assetService;
        _transactionService = transactionService;
        _quoteRefreshService = quoteRefreshService;
        _shellState = shellState;
        _dataInvalidation = dataInvalidation;
        _symbolSearchService = symbolSearchService;
        _notificationCenter = notificationCenter;
        _reportService = reportService;
        _taxService = taxService;
        _userContext = userContext;
        _exchangeRateProvider = exchangeRateProvider;

        ManualTransactionTypeOptions =
        [
            new ManualTransactionTypeOption("Покупка", TransactionType.Buy),
            new ManualTransactionTypeOption("Продажа", TransactionType.Sell),
        ];
        _selectedManualTransactionType = ManualTransactionTypeOptions[0];

        ManualTagOptions = ["Акции", "ETF", "Криптовалюта", "Облигации", "Валюта", "Наличность"];
        ManualSymbolSuggestions = [];
        VisibleAssets = [];

        _loadCommand = new AsyncCommand(LoadAsync, () => !IsBusy);
        _addManualTransactionCommand = new AsyncCommand(AddManualTransactionAsync, () => !IsBusy);
        _refreshQuotesCommand = new AsyncCommand(RefreshQuotesAsync, () => !IsBusy);
        _exportReportCommand = new AsyncCommand(ExportPortfolioReportAsync, () => CanExportReport);
        _openImportCommand = new DelegateCommand(_ => _navigation.Navigate(AppRoutes.ImportPreview));
        _moreAssetsCommand = new DelegateCommand(_ => ToggleAssetsLimit());
        _sortByNameCommand = new DelegateCommand(_ => SetSort(AssetSortMode.Name));
        _sortByShareCommand = new DelegateCommand(_ => SetSort(AssetSortMode.Share));
        _sortByValueCommand = new DelegateCommand(_ => SetSort(AssetSortMode.Value));
        _sortByChangeCommand = new DelegateCommand(_ => SetSort(AssetSortMode.Change24H));
        _selectManualBuyCommand = new DelegateCommand(_ => SelectManualTransactionType(TransactionType.Buy));
        _selectManualSellCommand = new DelegateCommand(_ => SelectManualTransactionType(TransactionType.Sell));
        _selectManualSymbolCommand = new DelegateCommand(SelectManualSymbol);

        _shellState.PortfolioChanged += (_, _) => _ = LoadAsync();
        _dataInvalidation.DataInvalidated += (_, _) => _ = LoadAsync();

        _ = LoadAsync();
    }

    public ObservableCollection<AssetListItemViewModel> VisibleAssets { get; }

    public ObservableCollection<MarketSymbolCandidate> ManualSymbolSuggestions { get; }

    public ObservableCollection<ManualTransactionTypeOption> ManualTransactionTypeOptions { get; }

    public ObservableCollection<string> ManualTagOptions { get; }

    public ICommand SelectManualBuyCommand => _selectManualBuyCommand;

    public ICommand SelectManualSellCommand => _selectManualSellCommand;

    public ICommand SelectManualSymbolCommand => _selectManualSymbolCommand;

    public bool IsManualBuySelected => SelectedManualTransactionType.Type == TransactionType.Buy;

    public bool IsManualSellSelected => SelectedManualTransactionType.Type == TransactionType.Sell;

    public string PageTitle => "Все активы";

    public string ImportTitle => "Синхронизируйте\nактивы";

    public string ImportDescription => "Наша система автоматически\nдобавит все транзакции из ваших\nотчётов. Загрузите их!";

    public string ManualTitle => "Ручной ввод транзакций";

    public string ManualDescription => "Оцифруйте физические транзакции при\nпомощи ручного ввода";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasContent));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _loadCommand.RaiseCanExecuteChanged();
                _addManualTransactionCommand.RaiseCanExecuteChanged();
                _refreshQuotesCommand.RaiseCanExecuteChanged();
                _exportReportCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(AddButtonText));
                OnPropertyChanged(nameof(RefreshButtonText));
                OnPropertyChanged(nameof(ExportReportButtonText));
                OnPropertyChanged(nameof(CanExportReport));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(HasContent));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public bool HasContent => !IsLoading && !HasError;

    public bool ShowEmptyState => !IsLoading && !HasError && _allAssets.Count == 0;

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyAssetFilterAndSort();
            }
        }
    }

    public string ManualAssetName
    {
        get => _manualAssetName;
        set
        {
            if (SetProperty(ref _manualAssetName, value))
            {
                ClearFormMessage();

                if (!_isApplyingManualSymbolSelection)
                {
                    if (_selectedManualSymbol is not null
                        && !string.Equals(value.Trim(), _selectedManualSymbol.Symbol, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(value.Trim(), _selectedManualSymbol.DisplaySymbol, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedManualSymbol = null;
                    }

                    _ = SearchManualSymbolsAsync(value);
                }
            }
        }
    }

    public string ManualIsinText
    {
        get => _manualIsinText;
        set
        {
            if (SetProperty(ref _manualIsinText, value))
            {
                ClearFormMessage();
            }
        }
    }

    public string ManualPriceText
    {
        get => _manualPriceText;
        set
        {
            if (SetProperty(ref _manualPriceText, value))
            {
                ClearFormMessage();
            }
        }
    }

    public string ManualQuantityText
    {
        get => _manualQuantityText;
        set
        {
            if (SetProperty(ref _manualQuantityText, value))
            {
                ClearFormMessage();
            }
        }
    }

    public string ManualDateText
    {
        get => _manualDateText;
        set
        {
            if (SetProperty(ref _manualDateText, value))
            {
                ClearFormMessage();
            }
        }
    }

    public string ManualTagText
    {
        get => _manualTagText;
        set
        {
            if (SetProperty(ref _manualTagText, value))
            {
                foreach (string tag in ParseTagList(value))
                {
                    EnsureManualTagOption(tag);
                }

                ClearFormMessage();
            }
        }
    }

    public ManualTransactionTypeOption SelectedManualTransactionType
    {
        get => _selectedManualTransactionType;
        set
        {
            if (SetProperty(ref _selectedManualTransactionType, value ?? ManualTransactionTypeOptions[0]))
            {
                OnPropertyChanged(nameof(IsManualBuySelected));
                OnPropertyChanged(nameof(IsManualSellSelected));
                ClearFormMessage();
            }
        }
    }

    public string FormMessage
    {
        get => _formMessage;
        private set
        {
            if (SetProperty(ref _formMessage, value))
            {
                OnPropertyChanged(nameof(HasFormMessage));
            }
        }
    }

    public bool HasFormMessage => !string.IsNullOrWhiteSpace(FormMessage);

    public string SymbolSearchMessage
    {
        get => _symbolSearchMessage;
        private set
        {
            if (SetProperty(ref _symbolSearchMessage, value))
            {
                OnPropertyChanged(nameof(HasSymbolSearchMessage));
            }
        }
    }

    public bool HasSymbolSearchMessage => !string.IsNullOrWhiteSpace(SymbolSearchMessage);

    public bool HasManualSymbolSuggestions => ManualSymbolSuggestions.Count > 0;

    public bool HasFormError
    {
        get => _hasFormError;
        private set => SetProperty(ref _hasFormError, value);
    }

    public string AddButtonText => IsBusy ? "Добавляем..." : "Добавить транзакцию";

    public string RefreshButtonText => IsBusy ? "Обновляем..." : "Обновить котировки";

    public string ExportReportButtonText => IsBusy ? "Формируем..." : "Скачать отчет";

    public bool CanExportReport => HasContent && !IsBusy && _allAssets.Count > 0;

    public string GrowthMetricValue { get; private set; } = "+0.0%";

    public string TotalValueMetric { get; private set; } = "$0.00";

    public string TaxesMetric { get; private set; } = "$0.00";

    public string MoreAssetsButtonText => _isShowingAllAssets ? "Скрыть активы  ↑" : "Больше активов  ↓";

    public ICommand LoadCommand => _loadCommand;

    public ICommand AddManualTransactionCommand => _addManualTransactionCommand;

    public ICommand RefreshQuotesCommand => _refreshQuotesCommand;

    public ICommand OpenImportCommand => _openImportCommand;

    public ICommand ExportReportCommand => _exportReportCommand;

    public ICommand MoreAssetsCommand => _moreAssetsCommand;

    public ICommand SortByNameCommand => _sortByNameCommand;

    public ICommand SortByShareCommand => _sortByShareCommand;

    public ICommand SortByValueCommand => _sortByValueCommand;

    public ICommand SortByChangeCommand => _sortByChangeCommand;

    private async Task LoadAsync()
    {
        IsBusy = true;
        IsLoading = true;
        HasError = false;
        ErrorText = string.Empty;

        try
        {
            Guid portfolioId = _shellState.CurrentPortfolioId;
            IReadOnlyList<Asset> assets = await _assetService.ListActiveAsync(portfolioId, CancellationToken.None).ConfigureAwait(true);
            _transactions = await _transactionService.ListActiveAsync(portfolioId, CancellationToken.None).ConfigureAwait(true);

            IReadOnlyDictionary<Guid, DerivedAssetSnapshot> derivedSnapshots = BuildDerivedAssetSnapshots(assets, _transactions);

            decimal totalValue = assets.Sum(asset =>
            {
                DerivedAssetSnapshot snapshot = derivedSnapshots[asset.Id];
                return snapshot.Quantity * snapshot.CurrentPrice;
            });

            decimal totalCost = assets.Sum(asset =>
            {
                DerivedAssetSnapshot snapshot = derivedSnapshots[asset.Id];
                return snapshot.Quantity * snapshot.AverageBuyPrice;
            });

            _currentPortfolioProfitLoss = totalValue - totalCost;
            decimal growth = totalCost <= 0m ? 0m : _currentPortfolioProfitLoss / totalCost * 100m;

            GrowthMetricValue = FormatPercent(growth, includeArrow: false);
            TotalValueMetric = FormatMoney(totalValue, "USD");
            StartCurrentTaxDueLoad(portfolioId);

            _allAssets = assets
                .Select(asset =>
                {
                    DerivedAssetSnapshot snapshot = derivedSnapshots[asset.Id];
                    Asset projected = asset with
                    {
                        Quantity = snapshot.Quantity,
                        AverageBuyPrice = snapshot.AverageBuyPrice,
                        CurrentPrice = snapshot.CurrentPrice,
                    };

                    return BuildAssetRow(projected, totalValue);
                })
                .Where(static item => item.Quantity > 0m)
                .ToArray();

            ApplyAssetFilterAndSort();
            RaiseSummaryProperties();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    private AssetListItemViewModel BuildAssetRow(Asset asset, decimal portfolioTotal)
    {
        decimal value = asset.Quantity * asset.CurrentPrice;
        decimal share = portfolioTotal <= 0m ? 0m : value / portfolioTotal * 100m;
        decimal change = asset.AverageBuyPrice <= 0m ? 0m : (asset.CurrentPrice - asset.AverageBuyPrice) / asset.AverageBuyPrice * 100m;

        return new AssetListItemViewModel(
            asset.Id,
            asset.Name,
            asset.Ticker,
            asset.Isin,
            asset.Type,
            asset.Currency,
            asset.Quantity,
            asset.CurrentPrice,
            value,
            share,
            change,
            asset.Tags,
            () => OpenAssetDetails(asset.Id, asset.Name),
            () => EditAsset(asset),
            () => _ = DeleteAssetAsync(asset.Id));
    }

    private static IReadOnlyDictionary<Guid, DerivedAssetSnapshot> BuildDerivedAssetSnapshots(
        IReadOnlyList<Asset> assets,
        IReadOnlyList<PortfolioTransaction> transactions)
    {
        Dictionary<Guid, List<PortfolioTransaction>> transactionsByAsset = transactions
            .Where(static tx => tx.AssetId.HasValue)
            .GroupBy(tx => tx.AssetId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(tx => tx.TradeDate).ToList());

        Dictionary<Guid, DerivedAssetSnapshot> result = new(assets.Count);
        foreach (Asset asset in assets)
        {
            if (!transactionsByAsset.TryGetValue(asset.Id, out List<PortfolioTransaction>? assetTransactions)
                || assetTransactions.Count == 0)
            {
                result[asset.Id] = new DerivedAssetSnapshot(asset.Quantity, asset.AverageBuyPrice, asset.CurrentPrice);
                continue;
            }

            decimal quantity = 0m;
            decimal averageBuyPrice = 0m;
            decimal currentPrice = asset.CurrentPrice;

            foreach (PortfolioTransaction tx in assetTransactions)
            {
                switch (tx.Type)
                {
                    case TransactionType.Buy:
                        {
                            decimal newQuantity = quantity + tx.Quantity;
                            averageBuyPrice = newQuantity <= 0m
                                ? tx.Price
                                : ((quantity * averageBuyPrice) + (tx.Quantity * tx.Price)) / newQuantity;
                            quantity = newQuantity;
                            break;
                        }

                    case TransactionType.Sell:
                        {
                            quantity = Math.Max(0m, quantity - tx.Quantity);
                            if (quantity == 0m)
                            {
                                averageBuyPrice = 0m;
                            }

                            break;
                        }
                }
            }

            if (quantity <= 0m && asset.Quantity > 0m)
            {
                quantity = asset.Quantity;
            }

            if (averageBuyPrice <= 0m && asset.AverageBuyPrice > 0m)
            {
                averageBuyPrice = asset.AverageBuyPrice;
            }

            if (currentPrice <= 0m)
            {
                currentPrice = asset.CurrentPrice;
            }

            result[asset.Id] = new DerivedAssetSnapshot(quantity, averageBuyPrice, currentPrice);
        }

        return result;
    }

    private async Task SearchManualSymbolsAsync(string query)
    {
        _manualSymbolSearchCts?.Cancel();
        _manualSymbolSearchCts?.Dispose();

        string trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            ManualSymbolSuggestions.Clear();
            SymbolSearchMessage = string.Empty;
            OnPropertyChanged(nameof(HasManualSymbolSuggestions));
            return;
        }

        CancellationTokenSource cts = new();
        _manualSymbolSearchCts = cts;

        try
        {
            await Task.Delay(250, cts.Token).ConfigureAwait(true);
            MarketSymbolSearchResult result = await _symbolSearchService
                .SearchAsync(trimmed, 8, cts.Token)
                .ConfigureAwait(true);

            if (cts.IsCancellationRequested || !ReferenceEquals(_manualSymbolSearchCts, cts))
            {
                return;
            }

            ManualSymbolSuggestions.Clear();
            if (!result.Succeeded)
            {
                SymbolSearchMessage = result.Message;
                OnPropertyChanged(nameof(HasManualSymbolSuggestions));
                return;
            }

            foreach (MarketSymbolCandidate item in result.Symbols)
            {
                ManualSymbolSuggestions.Add(item);
            }

            SymbolSearchMessage = result.Symbols.Count == 0 && trimmed.Length >= 2
                ? "Twelve Data не нашёл инструмент. Проверьте тикер или название."
                : string.Empty;
            OnPropertyChanged(nameof(HasManualSymbolSuggestions));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            SymbolSearchMessage = $"Ошибка поиска Twelve Data: {ex.Message}";
        }
    }

    private void SelectManualSymbol(object? parameter)
    {
        if (parameter is not MarketSymbolCandidate candidate)
        {
            return;
        }

        _selectedManualSymbol = candidate;
        _isApplyingManualSymbolSelection = true;
        try
        {
            ManualAssetName = candidate.Symbol;
            ManualTagText = candidate.AssetType switch
            {
                AssetType.Crypto => "Криптовалюта",
                AssetType.Etf => "ETF",
                AssetType.Bond => "Облигации",
                AssetType.Cash => "Наличность",
                AssetType.Currency => "Валюта",
                _ => "Акции",
            };
        }
        finally
        {
            _isApplyingManualSymbolSelection = false;
        }

        ManualSymbolSuggestions.Clear();
        SymbolSearchMessage = $"Выбран Twelve Data symbol: {candidate.Symbol}";
        OnPropertyChanged(nameof(HasManualSymbolSuggestions));
    }

    private async Task<MarketSymbolCandidate?> ResolveManualSymbolAsync(string input)
    {
        if (_selectedManualSymbol is not null
            && (string.Equals(input.Trim(), _selectedManualSymbol.Symbol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(input.Trim(), _selectedManualSymbol.DisplaySymbol, StringComparison.OrdinalIgnoreCase)))
        {
            return _selectedManualSymbol;
        }

        MarketSymbolSearchResult result = await _symbolSearchService
            .ResolveAsync(input, CancellationToken.None)
            .ConfigureAwait(true);

        if (!result.Succeeded || result.Symbols.Count == 0)
        {
            SetFormError(string.IsNullOrWhiteSpace(result.Message)
                ? "Выберите актив из подсказок Twelve Data."
                : result.Message);
            return null;
        }

        _selectedManualSymbol = result.Symbols[0];
        return _selectedManualSymbol;
    }

    private static AssetType ResolveManualAssetType(MarketSymbolCandidate symbol, IReadOnlyList<string> tags)
    {
        if (symbol.AssetType is AssetType.Cash || tags.Any(IsCashTag))
        {
            return AssetType.Cash;
        }

        if (tags.Any(IsCurrencyTag))
        {
            return AssetType.Currency;
        }

        return symbol.AssetType;
    }

    private static string ResolveManualTicker(MarketSymbolCandidate symbol, string rawInput, AssetType assetType)
    {
        string candidate = string.IsNullOrWhiteSpace(symbol.Symbol) ? rawInput : symbol.Symbol;
        candidate = MarketSymbolNormalizer.NormalizeForTwelveData(candidate);

        if (assetType is AssetType.Cash && candidate.Contains('/', StringComparison.Ordinal))
        {
            candidate = candidate.Split('/')[0];
        }

        return candidate.Trim().ToUpperInvariant();
    }

    private static string ResolveManualCurrency(MarketSymbolCandidate symbol, string ticker, AssetType assetType)
    {
        if (assetType is AssetType.Cash)
        {
            return "USD";
        }

        if (!string.IsNullOrWhiteSpace(symbol.Currency))
        {
            return symbol.Currency.Trim().ToUpperInvariant();
        }

        int slash = ticker.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 && slash + 1 < ticker.Length ? ticker[(slash + 1)..] : "USD";
    }

    private static string ResolveManualAssetName(MarketSymbolCandidate symbol, string ticker, AssetType assetType)
    {
        if (assetType is AssetType.Cash)
        {
            return $"{ticker} Cash";
        }

        return string.IsNullOrWhiteSpace(symbol.Description) ? symbol.PrimaryText : symbol.Description;
    }

    private static string? NormalizeManualIsin(string rawIsin, AssetType assetType)
    {
        if (assetType is AssetType.Cash or AssetType.Currency)
        {
            return null;
        }

        string normalized = new(rawIsin
            .Where(static character => !char.IsWhiteSpace(character) && character != '-')
            .Select(static character => char.ToUpperInvariant(character))
            .ToArray());

        return normalized.Length == 0 ? null : normalized;
    }

    private static decimal ResolveManualStoragePrice(string ticker, AssetType assetType, decimal enteredPrice)
    {
        if (assetType is AssetType.Cash && ticker.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        return enteredPrice;
    }

    private static bool IsCashTag(string tag)
    {
        string normalized = NormalizeTagForComparison(tag);
        return normalized.Equals("наличность", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("наличные", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("cash", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCurrencyTag(string tag)
    {
        string normalized = NormalizeTagForComparison(tag);
        return normalized.Equals("валюта", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("валюты", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("currency", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("fx", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ParseTagList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        char[] separators = [',', ';', '|'];
        return raw
            .Split(separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeTag)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeTag(string value)
    {
        string normalized = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.ToLowerInvariant() switch
        {
            "stock" or "stocks" or "equity" or "equities" or "share" or "shares" or "акции" or "акция" => "Акции",
            "etf" or "фонды" or "фонд" => "ETF",
            "crypto" or "cryptocurrency" or "крипта" or "криптовалюта" or "криптовалюты" => "Криптовалюта",
            "bond" or "bonds" or "облигация" or "облигации" => "Облигации",
            "currency" or "currencies" or "fx" or "валюта" or "валюты" => "Валюта",
            "cash" or "наличные" or "наличность" => "Наличность",
            "dividend" or "dividends" or "дивиденд" or "дивиденды" => "Дивиденды",
            _ => normalized,
        };
    }

    private static string NormalizeTagForComparison(string value)
    {
        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }

    private void EnsureManualTagOption(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        string normalized = tag.Trim();
        if (ManualTagOptions.Any(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        ManualTagOptions.Add(normalized);
    }

    private void SelectManualTransactionType(TransactionType type)
    {
        ManualTransactionTypeOption? option = ManualTransactionTypeOptions.FirstOrDefault(item => item.Type == type);
        if (option is not null)
        {
            SelectedManualTransactionType = option;
        }
    }

    private async Task AddManualTransactionAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearFormMessage();

        if (_shellState.CurrentPortfolioId == Guid.Empty)
        {
            SetFormError("Портфель не выбран.");
            return;
        }

        string assetName = ManualAssetName.Trim();
        if (string.IsNullOrWhiteSpace(assetName))
        {
            SetFormError("Введите название актива.");
            return;
        }

        string[] tags = ParseTagList(ManualTagText);
        if (tags.Length == 0)
        {
            SetFormError("Введите хотя бы один тег актива, например Акции, Дивиденды или AI. Несколько тегов разделяйте запятыми.");
            return;
        }

        if (!TryParseDecimal(ManualPriceText, out decimal price) || price <= 0m)
        {
            SetFormError("Введите цену больше нуля.");
            return;
        }

        if (!TryParseDecimal(ManualQuantityText, out decimal quantity) || quantity <= 0m)
        {
            SetFormError("Введите количество больше нуля.");
            return;
        }

        if (!TryParseDate(ManualDateText, out DateTimeOffset tradeDate))
        {
            SetFormError("Введите дату в формате ДД.ММ.ГГГГ.");
            return;
        }

        IsBusy = true;
        try
        {
            TransactionType transactionType = SelectedManualTransactionType.Type;
            MarketSymbolCandidate? symbol = await ResolveManualSymbolAsync(assetName).ConfigureAwait(true);
            if (symbol is null)
            {
                return;
            }

            IReadOnlyList<Asset> assets = await _assetService.ListActiveAsync(_shellState.CurrentPortfolioId, CancellationToken.None).ConfigureAwait(true);
            AssetType assetType = ResolveManualAssetType(symbol, tags);
            string ticker = ResolveManualTicker(symbol, assetName, assetType);
            string currency = ResolveManualCurrency(symbol, ticker, assetType);
            string resolvedAssetName = ResolveManualAssetName(symbol, ticker, assetType);
            decimal storagePrice = ResolveManualStoragePrice(ticker, assetType, price);
            string? normalizedIsin = NormalizeManualIsin(ManualIsinText, assetType);
            Asset? existing = assets.FirstOrDefault(asset =>
                string.Equals(asset.Ticker, ticker, StringComparison.OrdinalIgnoreCase)
                || string.Equals(asset.Name, resolvedAssetName, StringComparison.OrdinalIgnoreCase));

            if (transactionType == TransactionType.Sell && existing is null)
            {
                SetFormError("Для продажи актив уже должен существовать в портфеле.");
                return;
            }

            AssetOperationResult assetResult;
            if (existing is null)
            {
                assetResult = await _assetService.CreateAsync(new CreateAssetRequest(
                    _shellState.CurrentPortfolioId,
                    ticker,
                    resolvedAssetName,
                    assetType,
                    currency,
                    symbol.Exchange,
                    normalizedIsin,
                    tags,
                    quantity,
                    storagePrice,
                    storagePrice), CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                decimal newQuantity;
                decimal newAverage;

                if (transactionType == TransactionType.Buy)
                {
                    newQuantity = existing.Quantity + quantity;
                    newAverage = newQuantity <= 0m
                        ? storagePrice
                        : ((existing.Quantity * existing.AverageBuyPrice) + (quantity * storagePrice)) / newQuantity;
                }
                else
                {
                    if (quantity > existing.Quantity)
                    {
                        SetFormError($"Нельзя продать {quantity:N4}, потому что в портфеле только {existing.Quantity:N4}.");
                        return;
                    }

                    newQuantity = existing.Quantity - quantity;
                    newAverage = existing.AverageBuyPrice;
                }

                string[] mergedTags = existing.Tags
                    .Concat(tags)
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .Select(static item => item.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                assetResult = await _assetService.UpdateAsync(new UpdateAssetRequest(
                    existing.PortfolioId,
                    existing.Id,
                    existing.Ticker,
                    existing.Name,
                    existing.Type,
                    existing.Currency,
                    existing.Exchange,
                    normalizedIsin ?? existing.Isin,
                    mergedTags,
                    newQuantity,
                    newAverage,
                    storagePrice), CancellationToken.None).ConfigureAwait(true);
            }

            if (!assetResult.Succeeded || assetResult.Asset is null)
            {
                SetFormError(string.IsNullOrWhiteSpace(assetResult.Message) ? "Не удалось сохранить актив." : assetResult.Message);
                return;
            }

            decimal grossAmount = quantity * storagePrice;
            TransactionOperationResult txResult = await _transactionService.CreateAsync(new CreateTransactionRequest(
                _shellState.CurrentPortfolioId,
                assetResult.Asset.Id,
                transactionType,
                tradeDate,
                quantity,
                storagePrice,
                grossAmount,
                0m,
                0m,
                assetResult.Asset.Currency,
                null,
                null,
                $"Ручной ввод через экран активов. Теги: {string.Join(", ", tags)}"), CancellationToken.None).ConfigureAwait(true);

            if (!txResult.Succeeded)
            {
                SetFormError(string.IsNullOrWhiteSpace(txResult.Message) ? "Не удалось сохранить транзакцию." : txResult.Message);
                return;
            }

            _selectedManualSymbol = null;
            ManualSymbolSuggestions.Clear();
            OnPropertyChanged(nameof(HasManualSymbolSuggestions));
            ManualAssetName = string.Empty;
            ManualPriceText = string.Empty;
            ManualQuantityText = "1";
            ManualIsinText = string.Empty;
            ManualTagText = string.Join(", ", tags);
            foreach (string item in tags)
            {
                EnsureManualTagOption(item);
            }
            ManualDateText = DateTimeOffset.Now.ToString("dd.MM.yyyy", RuCulture);

            string actionText = transactionType == TransactionType.Buy ? "Покупка добавлена" : "Продажа добавлена";
            SetFormSuccess($"{actionText}. Активы и дашборд пересчитаны.");
            _dataInvalidation.Invalidate("manual-asset-transaction-created");
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetFormError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshQuotesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            QuoteRefreshSummary summary = await _quoteRefreshService.RefreshPortfolioAsync(_shellState.CurrentPortfolioId, CancellationToken.None).ConfigureAwait(true);
            if (summary.UpdatedCount == 0 && summary.CachedCount == 0 && summary.FailedCount > 0)
            {
                SetFormError(summary.Message);
                return;
            }

            if (summary.FailedCount > 0)
            {
                SetFormError(summary.Message);
            }
            else
            {
                SetFormSuccess(summary.Message);
            }

            _dataInvalidation.Invalidate("quotes-refreshed-from-assets-screen");
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetFormError($"Не удалось обновить котировки: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void EditAsset(Asset asset)
    {
        _selectedManualSymbol = null;
        ManualAssetName = asset.Ticker;
        ManualPriceText = asset.CurrentPrice.ToString(CultureInfo.InvariantCulture);
        ManualQuantityText = asset.Quantity.ToString(CultureInfo.InvariantCulture);
        ManualIsinText = asset.Isin ?? string.Empty;
        ManualTagText = asset.Tags.Count > 0
            ? string.Join(", ", asset.Tags)
            : asset.Type switch
            {
                AssetType.Crypto => "Криптовалюта",
                AssetType.Currency or AssetType.Cash => "Наличность",
                AssetType.Bond => "Облигации",
                AssetType.Etf => "ETF",
                _ => "Акции",
            };
        SelectedManualTransactionType = ManualTransactionTypeOptions[0];
        SetFormSuccess("Данные актива перенесены в форму. Измените значения и добавьте корректирующую транзакцию.");
    }

    private async Task DeleteAssetAsync(Guid assetId)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            AssetOperationResult result = await _assetService.ArchiveAsync(_shellState.CurrentPortfolioId, assetId, CancellationToken.None).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                SetFormError(string.IsNullOrWhiteSpace(result.Message) ? "Не удалось удалить актив." : result.Message);
                return;
            }

            SetFormSuccess("Актив удалён.");
            _dataInvalidation.Invalidate("asset-archived");
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetFormError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenAssetDetails(Guid assetId, string assetName)
    {
        _navigation.Navigate(
            AppRoutes.AssetDetails,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["assetId"] = assetId.ToString(),
            },
            titleOverride: assetName,
            breadcrumbOverride: $"Все активы / {assetName}");
    }

    private void ToggleAssetsLimit()
    {
        _isShowingAllAssets = !_isShowingAllAssets;
        OnPropertyChanged(nameof(MoreAssetsButtonText));
        ApplyAssetFilterAndSort();
    }

    private void SetSort(AssetSortMode sortMode)
    {
        if (_sortMode == sortMode)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortMode = sortMode;
            _sortDescending = sortMode is not AssetSortMode.Name;
        }

        ApplyAssetFilterAndSort();
    }

    private void ApplyAssetFilterAndSort()
    {
        IEnumerable<AssetListItemViewModel> query = _allAssets;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string term = SearchQuery.Trim();
            query = query.Where(item =>
                item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Ticker.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.TypeLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = _sortMode switch
        {
            AssetSortMode.Name => _sortDescending
                ? query.OrderByDescending(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase),
            AssetSortMode.Share => _sortDescending
                ? query.OrderByDescending(static item => item.Share)
                : query.OrderBy(static item => item.Share),
            AssetSortMode.Change24H => _sortDescending
                ? query.OrderByDescending(static item => item.Change24H)
                : query.OrderBy(static item => item.Change24H),
            _ => _sortDescending
                ? query.OrderByDescending(static item => item.Value)
                : query.OrderBy(static item => item.Value),
        };

        if (!_isShowingAllAssets)
        {
            query = query.Take(CompactAssetLimit);
        }

        VisibleAssets.Clear();
        foreach (AssetListItemViewModel item in query)
        {
            VisibleAssets.Add(item);
        }

        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void RaiseSummaryProperties()
    {
        OnPropertyChanged(nameof(GrowthMetricValue));
        OnPropertyChanged(nameof(TotalValueMetric));
        OnPropertyChanged(nameof(TaxesMetric));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(CanExportReport));
        _exportReportCommand.RaiseCanExecuteChanged();
    }

    private void StartCurrentTaxDueLoad(Guid portfolioId)
    {
        CancellationTokenSource? previous = _taxMetricLoadCts;
        previous?.Cancel();

        CancellationTokenSource cts = new();
        _taxMetricLoadCts = cts;

        TaxesMetric = "Расчёт...";
        OnPropertyChanged(nameof(TaxesMetric));

        _ = LoadCurrentTaxDueUsdInBackgroundAsync(portfolioId, _userContext.UserId, DateTime.UtcNow.Year, cts);
    }

    private async Task LoadCurrentTaxDueUsdInBackgroundAsync(
        Guid portfolioId,
        Guid userId,
        int year,
        CancellationTokenSource cts)
    {
        CancellationToken cancellationToken = cts.Token;

        try
        {
            decimal taxesUsd = await LoadCurrentTaxDueUsdAsync(portfolioId, userId, year, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested ||
                    !ReferenceEquals(_taxMetricLoadCts, cts) ||
                    _shellState.CurrentPortfolioId != portfolioId)
                {
                    return;
                }

                TaxesMetric = FormatMoney(taxesUsd, "USD");
                OnPropertyChanged(nameof(TaxesMetric));
            });
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested ||
                    !ReferenceEquals(_taxMetricLoadCts, cts) ||
                    _shellState.CurrentPortfolioId != portfolioId)
                {
                    return;
                }

                TaxesMetric = "Недоступно";
                OnPropertyChanged(nameof(TaxesMetric));
            });
        }
        finally
        {
            if (ReferenceEquals(_taxMetricLoadCts, cts))
            {
                _taxMetricLoadCts = null;
            }

            cts.Dispose();
        }
    }

    private async Task<decimal> LoadCurrentTaxDueUsdAsync(
        Guid portfolioId,
        Guid userId,
        int year,
        CancellationToken cancellationToken)
    {
        try
        {
            TaxOverview taxModel = await _taxService
                .GetOverviewAsync(portfolioId, userId, year, cancellationToken)
                .ConfigureAwait(false);
            if (taxModel.IsEmpty || taxModel.TotalTaxDue <= 0m)
            {
                return 0m;
            }

            if (taxModel.Currency.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                return taxModel.TotalTaxDue;
            }

            DateOnly rateDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            ExchangeRateResult usdRate = await _exchangeRateProvider
                .GetRateAsync("USD", taxModel.Currency, rateDate, cancellationToken)
                .ConfigureAwait(false);

            if (!usdRate.Succeeded || usdRate.Rate <= 0m)
            {
                return 0m;
            }

            return decimal.Round(taxModel.TotalTaxDue / usdRate.Rate, 2);
        }
        catch (Exception)
        {
            return 0m;
        }
    }

    private async Task ExportPortfolioReportAsync()
    {
        if (!CanExportReport)
        {
            await _notificationCenter.NotifyAsync(
                AppNotificationLevel.Warning,
                "Отчет не сформирован",
                "Сначала дождитесь загрузки таблицы активов и проверьте, что в портфеле есть активы.",
                "Все активы").ConfigureAwait(true);
            return;
        }

        IsBusy = true;
        try
        {
            PortfolioReportRequest request = BuildPortfolioReportRequest();
            ReportExportResult result = await _reportService.ExportPortfolioPdfAsync(request, CancellationToken.None).ConfigureAwait(true);

            if (result.Succeeded)
            {
                string message = string.IsNullOrWhiteSpace(result.OutputPath)
                    ? "PDF-отчет по текущему портфелю сформирован."
                    : $"PDF-отчет по текущему портфелю сохранен: {result.OutputPath}";

                await _notificationCenter.NotifyAsync(AppNotificationLevel.Success, "Отчет сформирован", message, "Все активы").ConfigureAwait(true);
                return;
            }

            await _notificationCenter.NotifyAsync(AppNotificationLevel.Error, "Отчет не сформирован", result.Message, "Все активы").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _notificationCenter.NotifyAsync(AppNotificationLevel.Error, "Ошибка экспорта отчета", ex.Message, "Все активы").ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private PortfolioReportRequest BuildPortfolioReportRequest()
    {
        string portfolioName = string.IsNullOrWhiteSpace(_shellState.CurrentPortfolioName)
            ? "Основной портфель"
            : _shellState.CurrentPortfolioName;

        decimal totalValue = _allAssets.Sum(static item => item.Value);
        IReadOnlyList<(string Category, decimal Value)> allocation = _allAssets
            .GroupBy(static item => item.TypeLabel, StringComparer.OrdinalIgnoreCase)
            .Select(static group => (Category: group.Key, Value: decimal.Round(group.Sum(item => item.Value), 2)))
            .OrderByDescending(static item => item.Value)
            .ToArray();

        IReadOnlyList<(string Asset, decimal Value)> topAssets = _allAssets
            .OrderByDescending(static item => item.Value)
            .Take(8)
            .Select(static item => (Asset: $"{item.Ticker} — {item.Name}", Value: decimal.Round(item.Value, 2)))
            .ToArray();

        IReadOnlyList<(string Metric, string Value)> portfolioMetrics =
        [
            ("Прирост", GrowthMetricValue),
            ("Налоги к уплате", TaxesMetric),
            ("Активов", _allAssets.Count.ToString(CultureInfo.InvariantCulture)),
            ("Операций", _transactions.Count.ToString(CultureInfo.InvariantCulture)),
            ("Стоимость", TotalValueMetric),
        ];

        IReadOnlyList<PortfolioReportAsset> assetRows = _allAssets
            .OrderByDescending(static item => item.Value)
            .Select(static item => new PortfolioReportAsset(
                item.Ticker,
                item.Name,
                item.TypeLabel,
                item.QuantityText,
                item.CurrentPriceText,
                item.ValueText,
                item.ShareText,
                item.Change24HText))
            .ToArray();

        return new PortfolioReportRequest(
            PortfolioName: portfolioName,
            PeriodLabel: $"Срез All assets на {DateTime.Now.ToString("dd.MM.yyyy HH:mm", RuCulture)}",
            TotalValue: decimal.Round(totalValue, 2),
            ProfitLoss: decimal.Round(_currentPortfolioProfitLoss, 2),
            Allocation: allocation,
            TopAssets: topAssets,
            RiskMetrics: portfolioMetrics,
            TransactionCount: _transactions.Count,
            Currency: "USD",
            Disclaimer: "Отчет сформирован по текущему портфелю на странице All assets. Сумма налогов синхронизирована с разделом Налоги и показана в USD по текущему курсу. Документ носит информационный характер и не является инвестиционной или налоговой консультацией.",
            OutputDirectory: string.Empty,
            AssetRows: assetRows);
    }

    private void SetFormError(string message)
    {
        HasFormError = true;
        FormMessage = message;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _ = _notificationCenter.NotifyAsync(AppNotificationLevel.Error, "Ошибка активов", message, "Все активы");
        }
    }

    private void SetFormSuccess(string message)
    {
        HasFormError = false;
        FormMessage = message;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _ = _notificationCenter.NotifyAsync(AppNotificationLevel.Success, "Операция выполнена", message, "Все активы");
        }
    }

    private void ClearFormMessage()
    {
        if (string.IsNullOrWhiteSpace(FormMessage))
        {
            return;
        }

        HasFormError = false;
        FormMessage = string.Empty;
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        string normalized = value.Trim().Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryParseDate(string value, out DateTimeOffset result)
    {
        string trimmed = value.Trim();
        string[] formats = ["dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d.M.yyyy"];
        if (DateTime.TryParseExact(trimmed, formats, RuCulture, DateTimeStyles.AssumeLocal, out DateTime exact))
        {
            result = new DateTimeOffset(exact.Date);
            return true;
        }

        if (DateTimeOffset.TryParse(trimmed, RuCulture, DateTimeStyles.AssumeLocal, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    private static string FormatMoney(decimal value, string currency)
    {
        string symbol = currency.ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "BYN" => "BYN ",
            "RUB" => "₽",
            "BTC" => "₿",
            _ => $"{currency.ToUpperInvariant()} ",
        };

        return string.Create(CultureInfo.InvariantCulture, $"{symbol}{value:N2}");
    }

    private static string FormatPercent(decimal value, bool includeArrow)
    {
        string sign = value > 0m ? "+" : string.Empty;
        string arrow = value switch
        {
            > 0m => "↑ ",
            < 0m => "↓ ",
            _ => "→ ",
        };

        return includeArrow
            ? string.Create(CultureInfo.InvariantCulture, $"{arrow}{sign}{value:N1}%")
            : string.Create(CultureInfo.InvariantCulture, $"{sign}{value:N1}%");
    }


    private readonly record struct DerivedAssetSnapshot(decimal Quantity, decimal AverageBuyPrice, decimal CurrentPrice);

    private enum AssetSortMode
    {
        Name,
        Share,
        Value,
        Change24H,
    }


}

public sealed class ManualTransactionTypeOption(string label, TransactionType type)
{
    public string Label { get; } = label;

    public TransactionType Type { get; } = type;

    public override string ToString() => Label;
}

public sealed class AssetListItemViewModel : ViewModelBase
{
    private readonly Action _open;
    private readonly Action _edit;
    private readonly Action _delete;

    public AssetListItemViewModel(
        Guid id,
        string name,
        string ticker,
        string? isin,
        AssetType type,
        string currency,
        decimal quantity,
        decimal currentPrice,
        decimal value,
        decimal share,
        decimal change24H,
        IReadOnlyList<string> tags,
        Action open,
        Action edit,
        Action delete)
    {
        Id = id;
        Name = name;
        Ticker = ticker;
        Isin = isin;
        Type = type;
        Currency = currency;
        Quantity = quantity;
        CurrentPrice = currentPrice;
        Value = value;
        Share = share;
        Change24H = change24H;
        Tags = tags;
        _open = open;
        _edit = edit;
        _delete = delete;
        OpenCommand = new DelegateCommand(_ => _open());
        EditCommand = new DelegateCommand(_ => _edit());
        DeleteCommand = new DelegateCommand(_ => _delete());
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Ticker { get; }

    public string? Isin { get; }

    public string AssetSubtitle
    {
        get
        {
            List<string> parts = [Name];
            if (!string.IsNullOrWhiteSpace(Isin))
            {
                parts.Add($"ISIN: {Isin}");
            }

            if (!string.IsNullOrWhiteSpace(TagsText))
            {
                parts.Add(TagsText);
            }

            return string.Join(" · ", parts);
        }
    }

    public AssetType Type { get; }

    public string Currency { get; }

    public decimal Quantity { get; }

    public decimal CurrentPrice { get; }

    public decimal Value { get; }

    public decimal Share { get; }

    public decimal Change24H { get; }

    public IReadOnlyList<string> Tags { get; }

    public string TagsText => Tags.Count == 0 ? string.Empty : string.Join(", ", Tags.Select(static tag => $"#{tag}"));

    public ICommand OpenCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public string TypeLabel => Type switch
    {
        AssetType.Stock => "Акции",
        AssetType.Crypto => "Криптовалюта",
        AssetType.Currency => "Валюта",
        AssetType.Cash => "Наличность",
        AssetType.Bond => "Облигации",
        AssetType.Etf => "ETF",
        _ => Type.ToString(),
    };

    public string IconText => Type switch
    {
        AssetType.Crypto => Ticker.Equals("BTC", StringComparison.OrdinalIgnoreCase) ? "₿" : Ticker[..Math.Min(1, Ticker.Length)],
        AssetType.Etf => Ticker.Contains("SP", StringComparison.OrdinalIgnoreCase) || Name.Contains("S&P", StringComparison.OrdinalIgnoreCase) ? "S&P" : "ETF",
        AssetType.Stock => Ticker[..Math.Min(1, Ticker.Length)],
        AssetType.Bond => "◆",
        AssetType.Currency or AssetType.Cash => "$",
        _ => Ticker[..Math.Min(1, Ticker.Length)],
    };

    public bool IsCrypto => Type == AssetType.Crypto;

    public bool IsEtf => Type == AssetType.Etf;

    public bool IsStock => Type == AssetType.Stock;

    public bool IsCash => Type is AssetType.Cash or AssetType.Currency;

    public bool IsBond => Type == AssetType.Bond;

    public string ShareText => string.Create(CultureInfo.InvariantCulture, $"{Share:N1}%");

    public double ShareBarWidth => Math.Clamp((double)Share * 2.2, Share > 0m ? 8 : 0, 126);

    public string CurrentPriceText => FormatMoney(CurrentPrice, PriceCurrency);

    public string QuantityText => Type switch
    {
        AssetType.Crypto => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N4} {Ticker}"),
        AssetType.Stock or AssetType.Etf => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} Shares"),
        AssetType.Bond => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} Units"),
        AssetType.Cash => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} {CashQuantitySymbol}"),
        AssetType.Currency => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} {CashQuantitySymbol}"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2}"),
    };

    public string ValueText => FormatMoney(Value, PriceCurrency);

    private string PriceCurrency => IsCash ? "USD" : Currency;

    private string CashQuantitySymbol
    {
        get
        {
            string ticker = Ticker.Trim().ToUpperInvariant();
            int slash = ticker.IndexOf('/', StringComparison.Ordinal);
            if (slash > 0)
            {
                return ticker[..slash];
            }

            return string.IsNullOrWhiteSpace(ticker) ? Currency : ticker;
        }
    }

    public string Change24HText
    {
        get
        {
            string sign = Change24H > 0m ? "+" : string.Empty;
            string arrow = Change24H switch
            {
                > 0m => "↑ ",
                < 0m => "↓ ",
                _ => "→ ",
            };
            return string.Create(CultureInfo.InvariantCulture, $"{arrow}{sign}{Change24H:N1}%");
        }
    }

    public bool IsPositive24H => Change24H >= 0m;

    public bool IsNegative24H => Change24H < 0m;

    private static string FormatMoney(decimal value, string currency)
    {
        string symbol = currency.ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "BYN" => "BYN ",
            "RUB" => "₽",
            "BTC" => "₿",
            _ => $"{currency.ToUpperInvariant()} ",
        };

        return string.Create(CultureInfo.InvariantCulture, $"{symbol}{value:N2}");
    }

}
