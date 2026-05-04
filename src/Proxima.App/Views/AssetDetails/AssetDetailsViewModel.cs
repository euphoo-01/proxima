using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.ViewModels;

namespace Proxima.App.Views.AssetDetails;

public sealed class AssetDetailsViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;
    private readonly IAssetDetailsReadModelProvider _provider;
    private readonly DelegateCommand _sortByDateCommand;
    private readonly DelegateCommand _sortByTypeCommand;
    private readonly DelegateCommand _sortByPriceCommand;
    private readonly DelegateCommand _sortByQuantityCommand;
    private readonly DelegateCommand _sortByAmountCommand;
    private readonly DelegateCommand _resetChartZoomCommand;

    private IReadOnlyList<AssetTransactionRowViewModel> _allTransactions = [];
    private string _searchQuery = string.Empty;
    private string _sortKey = "date";
    private bool _sortDescending = true;
    private bool _isLoading;
    private bool _isNotFound;
    private bool _hasError;
    private string _errorText = string.Empty;
    private int _candlesVisibleStartIndex;
    private int _candlesVisibleEndIndex;

    public AssetDetailsViewModel(IAppNavigationService navigation, IAssetDetailsReadModelProvider provider)
    {
        _navigation = navigation;
        _provider = provider;

        Timeframes = new ObservableCollection<string> { "1ч", "1д", "7д", "30д" };
        Candles = new ObservableCollection<CandlestickPointViewModel>();
        BasicMetrics = new ObservableCollection<AssetMetricItemViewModel>();
        RiskMetrics = new ObservableCollection<AssetMetricItemViewModel>();
        VisibleTransactions = new ObservableCollection<AssetTransactionRowViewModel>();

        _sortByDateCommand = new DelegateCommand(_ => SortBy("date"));
        _sortByTypeCommand = new DelegateCommand(_ => SortBy("type"));
        _sortByPriceCommand = new DelegateCommand(_ => SortBy("price"));
        _sortByQuantityCommand = new DelegateCommand(_ => SortBy("quantity"));
        _sortByAmountCommand = new DelegateCommand(_ => SortBy("amount"));
        _resetChartZoomCommand = new DelegateCommand(_ => ResetCandlesVisibleRange());

        _navigation.RouteChanged += HandleRouteChanged;
    }

    public AssetDetailsViewModel(IAppNavigationService navigation, IAssetDetailsReadModelProvider provider, IRuntimeDataInvalidation dataInvalidation)
        : this(navigation, provider)
    {
        dataInvalidation.DataInvalidated += (_, _) => LoadByRoute(_navigation.Current);
    }

    public ObservableCollection<string> Timeframes { get; }

    public ObservableCollection<CandlestickPointViewModel> Candles { get; }

    public ObservableCollection<AssetMetricItemViewModel> BasicMetrics { get; }

    public ObservableCollection<AssetMetricItemViewModel> RiskMetrics { get; }

    public ObservableCollection<AssetTransactionRowViewModel> VisibleTransactions { get; }

    public ICommand SortByDateCommand => _sortByDateCommand;

    public ICommand SortByTypeCommand => _sortByTypeCommand;

    public ICommand SortByPriceCommand => _sortByPriceCommand;

    public ICommand SortByQuantityCommand => _sortByQuantityCommand;

    public ICommand SortByAmountCommand => _sortByAmountCommand;
    public ICommand ResetChartZoomCommand => _resetChartZoomCommand;

    public string AssetName { get; private set; } = "Asset";

    public string AssetTicker { get; private set; } = "---";

    public string PriceText { get; private set; } = "Нет данных";

    public string DeltaText { get; private set; } = "Недоступно";
    public string AssetCurrency { get; private set; } = "USD";

    public int CandlesVisibleStartIndex
    {
        get => _candlesVisibleStartIndex;
        set => SetProperty(ref _candlesVisibleStartIndex, value);
    }

    public int CandlesVisibleEndIndex
    {
        get => _candlesVisibleEndIndex;
        set => SetProperty(ref _candlesVisibleEndIndex, value);
    }

    public string SelectedTimeframe
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                LoadByRoute(_navigation.Current);
            }
        }
    } = "1д";

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyTransactionFilters();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasData));
                OnPropertyChanged(nameof(ShowEmptyChart));
                OnPropertyChanged(nameof(ShowTransactions));
                OnPropertyChanged(nameof(IsTransactionsEmpty));
            }
        }
    }

    public bool IsNotFound
    {
        get => _isNotFound;
        private set
        {
            if (SetProperty(ref _isNotFound, value))
            {
                OnPropertyChanged(nameof(HasData));
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(HasData));
            }
        }
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public bool HasData => !IsLoading && !IsNotFound && !HasError;

    public bool ShowEmptyChart => HasData && Candles.Count == 0;

    public bool ShowTransactions => HasData && VisibleTransactions.Count > 0;

    public bool IsTransactionsEmpty => HasData && VisibleTransactions.Count == 0;

    public static AssetDetailsViewModel CreateDesignData()
    {
        AppNavigationService navigation = new();
        navigation.Register(new AppRoute(AppRoutes.AssetDetails, "Asset Details", "Все активы / Apple Inc."));
        AssetDetailsViewModel viewModel = new(navigation, new MockAssetDetailsReadModelProvider());
        navigation.Navigate(
            AppRoutes.AssetDetails,
            new Dictionary<string, string> { ["assetId"] = MockAssetDetailsReadModelProvider.PrimaryAssetId.ToString() },
            "Apple Inc.",
            "Все активы / Apple Inc.");
        return viewModel;
    }

    private void HandleRouteChanged(AppRoute route)
    {
        if (!string.Equals(route.Key, AppRoutes.AssetDetails, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LoadByRoute(route);
    }

    private void LoadByRoute(AppRoute route)
    {
        IsLoading = true;
        HasError = false;
        IsNotFound = false;
        ErrorText = string.Empty;

        try
        {
            if (route.Parameters is null
                || !route.Parameters.TryGetValue("assetId", out string? assetIdRaw)
                || !Guid.TryParse(assetIdRaw, out Guid assetId))
            {
                IsNotFound = true;
                return;
            }

            AssetDetailsReadModel? model = _provider.Get(assetId, SelectedTimeframe);
            if (model is null)
            {
                IsNotFound = true;
                return;
            }

            AssetName = model.AssetName;
            AssetTicker = model.AssetTicker;
            PriceText = model.PriceText;
            DeltaText = model.DeltaText;
            AssetCurrency = model.CurrencyCode;

            Candles.Clear();
            foreach (CandlestickPointViewModel point in model.Candles)
            {
                Candles.Add(point);
            }
            ResetCandlesVisibleRange();

            BasicMetrics.Clear();
            foreach (AssetMetricItemViewModel metric in model.BasicMetrics)
            {
                BasicMetrics.Add(metric);
            }

            RiskMetrics.Clear();
            foreach (AssetMetricItemViewModel metric in model.RiskMetrics)
            {
                RiskMetrics.Add(metric);
            }

            _allTransactions = model.Transactions;
            ApplyTransactionFilters();

            OnPropertyChanged(nameof(AssetName));
            OnPropertyChanged(nameof(AssetTicker));
            OnPropertyChanged(nameof(PriceText));
            OnPropertyChanged(nameof(DeltaText));
            OnPropertyChanged(nameof(AssetCurrency));
            OnPropertyChanged(nameof(ShowEmptyChart));
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ResetCandlesVisibleRange()
    {
        if (Candles.Count == 0)
        {
            CandlesVisibleStartIndex = 0;
            CandlesVisibleEndIndex = 0;
            return;
        }

        CandlesVisibleStartIndex = 0;
        CandlesVisibleEndIndex = Candles.Count - 1;
    }

    private void SortBy(string key)
    {
        if (string.Equals(_sortKey, key, StringComparison.Ordinal))
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortKey = key;
            _sortDescending = true;
        }

        ApplyTransactionFilters();
    }

    private void ApplyTransactionFilters()
    {
        IEnumerable<AssetTransactionRowViewModel> query = _allTransactions;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string term = SearchQuery.Trim();
            query = query.Where(item =>
                item.TypeLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.DateText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = _sortKey switch
        {
            "type" => _sortDescending
                ? query.OrderByDescending(item => item.TypeLabel, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(item => item.TypeLabel, StringComparer.OrdinalIgnoreCase),
            "price" => _sortDescending ? query.OrderByDescending(item => item.Price) : query.OrderBy(item => item.Price),
            "quantity" => _sortDescending ? query.OrderByDescending(item => item.Quantity) : query.OrderBy(item => item.Quantity),
            "amount" => _sortDescending ? query.OrderByDescending(item => item.Amount) : query.OrderBy(item => item.Amount),
            _ => _sortDescending ? query.OrderByDescending(item => item.Date) : query.OrderBy(item => item.Date)
        };

        VisibleTransactions.Clear();
        foreach (AssetTransactionRowViewModel row in query)
        {
            VisibleTransactions.Add(row);
        }

        OnPropertyChanged(nameof(ShowTransactions));
        OnPropertyChanged(nameof(IsTransactionsEmpty));
    }

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record AssetMetricItemViewModel(string Label, string Value, string Hint, string Severity)
{
    public string SeverityLabel => Severity;
}

public sealed record AssetTransactionRowViewModel(DateTimeOffset Date, string TypeLabel, decimal Price, decimal Quantity, decimal Amount, string Currency)
{
    public string DateText => Date.LocalDateTime.ToString("dd.MM.yyyy");
    public string PriceText => $"{Price:N2} {Currency}";
    public string QuantityText => $"{Quantity:N4}";
    public string AmountText => $"{Amount:N2} {Currency}";
}

public sealed record AssetDetailsReadModel(
    Guid AssetId,
    string AssetName,
    string AssetTicker,
    string PriceText,
    string DeltaText,
    string CurrencyCode,
    IReadOnlyList<CandlestickPointViewModel> Candles,
    IReadOnlyList<AssetMetricItemViewModel> BasicMetrics,
    IReadOnlyList<AssetMetricItemViewModel> RiskMetrics,
    IReadOnlyList<AssetTransactionRowViewModel> Transactions);

public interface IAssetDetailsReadModelProvider
{
    AssetDetailsReadModel? Get(Guid assetId, string timeframe);
}

public sealed class MockAssetDetailsReadModelProvider : IAssetDetailsReadModelProvider
{
    public static readonly Guid PrimaryAssetId = Guid.Parse("0a896663-ec39-4ebc-9b28-bf537f8f2fe0");

    private static readonly Dictionary<Guid, (string Name, string Ticker)> AssetNames = new()
    {
        [PrimaryAssetId] = ("Apple Inc.", "AAPL"),
        [Guid.Parse("f3b4b129-5478-4965-923f-03ef74ca7c07")] = ("Microsoft", "MSFT"),
        [Guid.Parse("3ab7f07a-8c4b-44ec-9025-f4d0983e0fbf")] = ("Bitcoin", "BTC")
    };

    public AssetDetailsReadModel? Get(Guid assetId, string timeframe)
    {
        if (!AssetNames.TryGetValue(assetId, out (string Name, string Ticker) asset))
        {
            return null;
        }

        IReadOnlyList<CandlestickPointViewModel> candles = BuildCandles(timeframe);

        AssetMetricItemViewModel[] basicMetrics =
        [
            new("Market Cap", "2.89T USD", "Оценка рыночной капитализации", "low"),
            new("FDV", "2.94T USD", "Полностью разводнённая оценка", "low"),
            new("P/E or P/S", "28.4 P/E", "P/S отображается если P/E недоступен", "medium"),
            new("24h Volume", "81.2B USD", "Объём торгов за 24 часа", "low"),
            new("Circulating vs Total", "15.6B / 16.1B", "Текущий и общий объём обращения", "low"),
            new("SMA 50 / SMA 200", "182.10 / 176.45", "Скользящие средние закрытия", "medium"),
            new("RSI", "57.8", "RSI(14), нейтральная зона", "medium")
        ];

        AssetMetricItemViewModel[] riskMetrics =
        [
            new("Sharpe", "1.21", "Risk-adjusted доходность", "medium"),
            new("Sortino", "1.47", "Downside-risk adjusted", "medium"),
            new("Calmar", "0.93", "Доходность к max drawdown", "medium"),
            new("Max Drawdown", "-18.3%", "Максимальная просадка", "high"),
            new("VaR", "-3.9%", "95% историческая оценка", "medium"),
            new("CVaR", "-5.8%", "Усреднение хвоста распределения", "high"),
            new("Beta", "1.12", "Относительно индекса NASDAQ", "medium"),
            new("HV / IV", "24.6% / Недоступно", "IV ожидает внешний провайдер", "medium"),
            new("ATR", "3.42", "ATR(14), абсолютная волатильность", "medium"),
            new("Turnover", "0.74", "Оборот за период", "low"),
            new("Spread / Depth", "Недоступно", "Требуется orderbook feed", "high"),
            new("Hurst", "0.58", "Трендовость ряда", "medium"),
            new("Z-Score", "1.07", "Отклонение от среднего", "medium"),
            new("Correlation", "0.76", "Корреляция с benchmark", "medium")
        ];

        AssetTransactionRowViewModel[] transactions =
        [
            new(DateTimeOffset.UtcNow.AddDays(-2), "Buy", 186.2m, 2m, 372.4m, "USD"),
            new(DateTimeOffset.UtcNow.AddDays(-5), "Buy", 184.3m, 1.5m, 276.45m, "USD"),
            new(DateTimeOffset.UtcNow.AddDays(-10), "Sell", 188.1m, 0.7m, 131.67m, "USD"),
            new(DateTimeOffset.UtcNow.AddDays(-17), "Dividend", 0m, 0m, 12.50m, "USD")
        ];

        string priceText = asset.Ticker == "BTC" ? "68 534.23 USD" : "186.43 USD";
        string deltaText = asset.Ticker == "BTC" ? "+2.18%" : "+0.84%";

        return new AssetDetailsReadModel(
            assetId,
            asset.Name,
            asset.Ticker,
            priceText,
            deltaText,
            "USD",
            candles,
            basicMetrics,
            riskMetrics,
            transactions);
    }

    private static IReadOnlyList<CandlestickPointViewModel> BuildCandles(string timeframe)
    {
        decimal[] baseValues = timeframe switch
        {
            "1ч" => [186.1m, 186.4m, 186.0m, 186.3m, 186.6m, 186.5m, 186.7m, 186.4m],
            "7д" => [181m, 182.4m, 183.1m, 184.2m, 185m, 184.7m, 186.3m, 186.4m],
            "30д" => [172m, 174m, 176.3m, 178.9m, 181.2m, 183m, 185.1m, 186.4m],
            _ => [184.8m, 185.2m, 184.9m, 185.7m, 186.2m, 185.8m, 186.4m, 186.1m]
        };
        TimeSpan step = timeframe switch
        {
            "1ч" => TimeSpan.FromMinutes(5),
            "7д" => TimeSpan.FromDays(1),
            "30д" => TimeSpan.FromDays(3),
            _ => TimeSpan.FromHours(3)
        };
        DateTimeOffset start = DateTimeOffset.UtcNow - TimeSpan.FromTicks(step.Ticks * baseValues.Length);

        List<CandlestickPointViewModel> result = new(baseValues.Length);
        for (int i = 0; i < baseValues.Length; i++)
        {
            decimal open = baseValues[i];
            decimal close = open + (i % 2 == 0 ? 0.35m : -0.27m);
            decimal high = Math.Max(open, close) + 0.44m;
            decimal low = Math.Min(open, close) - 0.41m;
            decimal volume = 12_000m + (i * 790m);
            result.Add(new CandlestickPointViewModel(start + TimeSpan.FromTicks(step.Ticks * i), open, high, low, close, volume));
        }

        return result;
    }
}
