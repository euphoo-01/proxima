using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Media;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.Application.Assets;
using Proxima.Application.Quotes;
using Proxima.Application.Transactions;
using Proxima.Domain.Assets;
using Proxima.Domain.Transactions;

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
    private readonly AsyncCommand _loadCommand;
    private readonly AsyncCommand _addManualTransactionCommand;
    private readonly AsyncCommand _refreshQuotesCommand;
    private readonly DelegateCommand _openImportDialogCommand;
    private readonly DelegateCommand _moreAssetsCommand;
    private readonly DelegateCommand _sortByNameCommand;
    private readonly DelegateCommand _sortByShareCommand;
    private readonly DelegateCommand _sortByValueCommand;
    private readonly DelegateCommand _sortByChangeCommand;
    private readonly DelegateCommand _selectManualBuyCommand;
    private readonly DelegateCommand _selectManualSellCommand;
    private IReadOnlyList<AssetListItemViewModel> _allAssets = [];
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
    private string _manualDateText = DateTimeOffset.Now.ToString("dd.MM.yyyy", RuCulture);
    private string _formMessage = string.Empty;
    private bool _hasFormError;
    private string _manualTagText = "Акция";
    private ManualTransactionTypeOption _selectedManualTransactionType;
    private AssetSortMode _sortMode = AssetSortMode.Value;
    private bool _sortDescending = true;

    public AssetsViewModel(
        IAppNavigationService navigation,
        IAssetService assetService,
        ITransactionService transactionService,
        IQuoteRefreshService quoteRefreshService,
        IShellState shellState,
        IRuntimeDataInvalidation dataInvalidation)
    {
        _navigation = navigation;
        _assetService = assetService;
        _transactionService = transactionService;
        _quoteRefreshService = quoteRefreshService;
        _shellState = shellState;
        _dataInvalidation = dataInvalidation;

        ManualTransactionTypeOptions =
        [
            new ManualTransactionTypeOption("Покупка", TransactionType.Buy),
            new ManualTransactionTypeOption("Продажа", TransactionType.Sell),
        ];
        _selectedManualTransactionType = ManualTransactionTypeOptions[0];

        VisibleAssets = [];

        _loadCommand = new AsyncCommand(LoadAsync, () => !IsBusy);
        _addManualTransactionCommand = new AsyncCommand(AddManualTransactionAsync, () => !IsBusy);
        _refreshQuotesCommand = new AsyncCommand(RefreshQuotesAsync, () => !IsBusy);
        _openImportDialogCommand = new DelegateCommand(_ => _navigation.Navigate(AppRoutes.ImportPreview));
        _moreAssetsCommand = new DelegateCommand(_ => ToggleAssetsLimit());
        _sortByNameCommand = new DelegateCommand(_ => SetSort(AssetSortMode.Name));
        _sortByShareCommand = new DelegateCommand(_ => SetSort(AssetSortMode.Share));
        _sortByValueCommand = new DelegateCommand(_ => SetSort(AssetSortMode.Value));
        _sortByChangeCommand = new DelegateCommand(_ => SetSort(AssetSortMode.Change24H));
        _selectManualBuyCommand = new DelegateCommand(_ => SelectManualTransactionType(TransactionType.Buy));
        _selectManualSellCommand = new DelegateCommand(_ => SelectManualTransactionType(TransactionType.Sell));

        _shellState.PortfolioChanged += (_, _) => _ = LoadAsync();
        _dataInvalidation.DataInvalidated += (_, _) => _ = LoadAsync();

        _ = LoadAsync();
    }

    public ObservableCollection<AssetListItemViewModel> VisibleAssets { get; }

    public ObservableCollection<ManualTransactionTypeOption> ManualTransactionTypeOptions { get; }

    public ICommand SelectManualBuyCommand => _selectManualBuyCommand;

    public ICommand SelectManualSellCommand => _selectManualSellCommand;

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
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(AddButtonText));
                OnPropertyChanged(nameof(RefreshButtonText));
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

    public bool HasFormError
    {
        get => _hasFormError;
        private set => SetProperty(ref _hasFormError, value);
    }

    public string AddButtonText => IsBusy ? "Добавляем..." : "Добавить транзакцию";

    public string RefreshButtonText => IsBusy ? "Обновляем..." : "Обновить котировки";

    public string GrowthMetricValue { get; private set; } = "+0.0%";

    public string TotalValueMetric { get; private set; } = "$0.00";

    public string TaxesMetric { get; private set; } = "$0.00";

    public string MoreAssetsButtonText => _isShowingAllAssets ? "Скрыть активы⌃" : "Больше активов⌄";

    public ICommand LoadCommand => _loadCommand;

    public ICommand AddManualTransactionCommand => _addManualTransactionCommand;

    public ICommand RefreshQuotesCommand => _refreshQuotesCommand;

    public ICommand OpenImportDialogCommand => _openImportDialogCommand;

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

            decimal totalValue = assets.Sum(static asset => asset.Quantity * asset.CurrentPrice);
            decimal totalCost = assets.Sum(static asset => asset.Quantity * asset.AverageBuyPrice);
            decimal growth = totalCost <= 0m ? 0m : (totalValue - totalCost) / totalCost * 100m;
            decimal taxes = _transactions.Sum(static tx => tx.TaxAmount + (tx.Type == TransactionType.Tax ? tx.GrossAmount : 0m));

            GrowthMetricValue = FormatPercent(growth, includeArrow: false);
            TotalValueMetric = FormatMoney(totalValue, "USD");
            TaxesMetric = FormatMoney(taxes, "USD");

            _allAssets = assets
                .Select(asset => BuildAssetRow(asset, totalValue))
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
            asset.Type,
            asset.Currency,
            asset.Quantity,
            asset.CurrentPrice,
            value,
            share,
            change,
            () => OpenAssetDetails(asset.Id, asset.Name),
            () => EditAsset(asset),
            () => _ = DeleteAssetAsync(asset.Id));
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

        string tag = ManualTagText.Trim();
        if (string.IsNullOrWhiteSpace(tag))
        {
            SetFormError("Введите тег актива, например Акции, Криптовалюта или Наличность.");
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
            IReadOnlyList<Asset> assets = await _assetService.ListActiveAsync(_shellState.CurrentPortfolioId, CancellationToken.None).ConfigureAwait(true);
            AssetType assetType = InferAssetType(assetName, tag);
            string ticker = BuildTicker(assetName, assetType);
            Asset? existing = assets.FirstOrDefault(asset =>
                string.Equals(asset.Ticker, ticker, StringComparison.OrdinalIgnoreCase)
                || string.Equals(asset.Name, assetName, StringComparison.OrdinalIgnoreCase));

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
                    assetName,
                    assetType,
                    "USD",
                    null,
                    null,
                    [tag],
                    null,
                    quantity,
                    price,
                    price), CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                decimal newQuantity;
                decimal newAverage;

                if (transactionType == TransactionType.Buy)
                {
                    newQuantity = existing.Quantity + quantity;
                    newAverage = newQuantity <= 0m
                        ? price
                        : ((existing.Quantity * existing.AverageBuyPrice) + (quantity * price)) / newQuantity;
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

                string[] tags = existing.Tags
                    .Concat([tag])
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
                    existing.Isin,
                    tags,
                    null,
                    newQuantity,
                    newAverage,
                    price), CancellationToken.None).ConfigureAwait(true);
            }

            if (!assetResult.Succeeded || assetResult.Asset is null)
            {
                SetFormError(string.IsNullOrWhiteSpace(assetResult.Message) ? "Не удалось сохранить актив." : assetResult.Message);
                return;
            }

            decimal grossAmount = quantity * price;
            TransactionOperationResult txResult = await _transactionService.CreateAsync(new CreateTransactionRequest(
                _shellState.CurrentPortfolioId,
                assetResult.Asset.Id,
                transactionType,
                tradeDate,
                quantity,
                price,
                grossAmount,
                0m,
                0m,
                assetResult.Asset.Currency,
                null,
                null,
                $"Ручной ввод через экран активов. Тег: {tag}"), CancellationToken.None).ConfigureAwait(true);

            if (!txResult.Succeeded)
            {
                SetFormError(string.IsNullOrWhiteSpace(txResult.Message) ? "Не удалось сохранить транзакцию." : txResult.Message);
                return;
            }

            ManualAssetName = string.Empty;
            ManualPriceText = string.Empty;
            ManualQuantityText = "1";
            ManualTagText = tag;
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
            SetFormSuccess(summary.Message);
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
        ManualAssetName = asset.Name;
        ManualPriceText = asset.CurrentPrice.ToString(CultureInfo.InvariantCulture);
        ManualQuantityText = asset.Quantity.ToString(CultureInfo.InvariantCulture);
        ManualTagText = asset.Tags.FirstOrDefault() ?? asset.Type switch
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
    }

    private void SetFormError(string message)
    {
        HasFormError = true;
        FormMessage = message;
    }

    private void SetFormSuccess(string message)
    {
        HasFormError = false;
        FormMessage = message;
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

    private static AssetType InferAssetType(string assetName, string tag)
    {
        string source = $"{assetName} {tag}".ToLowerInvariant();

        if (source.Contains("крип", StringComparison.Ordinal)
            || source.Contains("crypto", StringComparison.Ordinal)
            || source.Contains("bitcoin", StringComparison.Ordinal)
            || source.Contains("btc", StringComparison.Ordinal)
            || source.Contains("ethereum", StringComparison.Ordinal)
            || source.Contains("eth", StringComparison.Ordinal))
        {
            return AssetType.Crypto;
        }

        if (source.Contains("etf", StringComparison.Ordinal)
            || source.Contains("s&p", StringComparison.Ordinal)
            || source.Contains("spy", StringComparison.Ordinal))
        {
            return AssetType.Etf;
        }

        if (source.Contains("облиг", StringComparison.Ordinal) || source.Contains("bond", StringComparison.Ordinal))
        {
            return AssetType.Bond;
        }

        if (source.Contains("валют", StringComparison.Ordinal)
            || source.Contains("cash", StringComparison.Ordinal)
            || source.Contains("кэш", StringComparison.Ordinal)
            || source.Contains("налич", StringComparison.Ordinal)
            || source.Contains("usd", StringComparison.Ordinal)
            || source.Contains("eur", StringComparison.Ordinal)
            || source.Contains("byn", StringComparison.Ordinal))
        {
            return AssetType.Currency;
        }

        return AssetType.Stock;
    }

    private static string BuildTicker(string assetName, AssetType type)
    {
        string normalized = assetName.Trim();
        string lower = normalized.ToLowerInvariant();

        if (lower.Contains("bitcoin", StringComparison.Ordinal) || lower is "btc")
        {
            return "BTC";
        }

        if (lower.Contains("ethereum", StringComparison.Ordinal) || lower is "eth")
        {
            return "ETH";
        }

        if (lower.Contains("apple", StringComparison.Ordinal))
        {
            return "AAPL";
        }

        if (lower.Contains("s&p", StringComparison.Ordinal) || lower.Contains("sp500", StringComparison.Ordinal))
        {
            return "SPY";
        }

        if (lower.Contains("золото", StringComparison.Ordinal) || lower.Contains("gold", StringComparison.Ordinal))
        {
            return "GOLD";
        }

        if ((type is AssetType.Currency or AssetType.Cash) && normalized.Length <= 4)
        {
            return normalized.ToUpperInvariant();
        }

        string ascii = Regex.Replace(RemoveDiacritics(normalized), "[^A-Za-z0-9]", string.Empty, RegexOptions.None, TimeSpan.FromMilliseconds(100));
        if (ascii.Length > 0)
        {
            return ascii[..Math.Min(8, ascii.Length)].ToUpperInvariant();
        }

        int hash = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(normalized));
        return $"A{hash % 100000:00000}";
    }

    private static string RemoveDiacritics(string text)
    {
        string normalized = text.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalized.Length);
        foreach (char c in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
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

    private enum AssetSortMode
    {
        Name,
        Share,
        Value,
        Change24H,
    }

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);
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
        AssetType type,
        string currency,
        decimal quantity,
        decimal currentPrice,
        decimal value,
        decimal share,
        decimal change24H,
        Action open,
        Action edit,
        Action delete)
    {
        Id = id;
        Name = name;
        Ticker = ticker;
        Type = type;
        Currency = currency;
        Quantity = quantity;
        CurrentPrice = currentPrice;
        Value = value;
        Share = share;
        Change24H = change24H;
        _open = open;
        _edit = edit;
        _delete = delete;
        OpenCommand = new RowCommand(_ => _open());
        EditCommand = new RowCommand(_ => _edit());
        DeleteCommand = new RowCommand(_ => _delete());
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Ticker { get; }

    public AssetType Type { get; }

    public string Currency { get; }

    public decimal Quantity { get; }

    public decimal CurrentPrice { get; }

    public decimal Value { get; }

    public decimal Share { get; }

    public decimal Change24H { get; }

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

    public string CurrentPriceText => FormatMoney(CurrentPrice, Currency);

    public string QuantityText => Type switch
    {
        AssetType.Crypto => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N4} {Ticker}"),
        AssetType.Stock or AssetType.Etf => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} Shares"),
        AssetType.Bond => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} Units"),
        AssetType.Currency or AssetType.Cash => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} {Currency}"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2}"),
    };

    public string ValueText => FormatMoney(Value, Currency);

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

    private sealed class RowCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}
