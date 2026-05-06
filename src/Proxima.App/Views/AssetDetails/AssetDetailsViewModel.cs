using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.Application.AssetDetails;

namespace Proxima.App.Views.AssetDetails;

public sealed class AssetDetailsViewModel : ViewModelBase
{
    private readonly IAppNavigationService _navigation;
    private readonly IAssetDetailsService _assetDetailsService;

    private readonly DelegateCommand _sortByDateCommand;
    private readonly DelegateCommand _sortByTypeCommand;
    private readonly DelegateCommand _sortByPriceCommand;
    private readonly DelegateCommand _sortByQuantityCommand;
    private readonly DelegateCommand _sortByAmountCommand;
    private readonly DelegateCommand _resetChartZoomCommand;
    private readonly DelegateCommand _reloadCommand;

    private IReadOnlyList<AssetTransactionRowViewModel> _allTransactions = [];
    private string _searchQuery = string.Empty;
    private string _sortKey = "date";
    private bool _sortDescending = true;
    private bool _isLoading;
    private bool _isNotFound;
    private bool _hasError;
    private string _errorText = string.Empty;
    private string _selectedTimeframe = "1д";
    private int _candlesVisibleStartIndex;
    private int _candlesVisibleEndIndex;

    public AssetDetailsViewModel(
        IAppNavigationService navigation,
        IAssetDetailsService assetDetailsService,
        IRuntimeDataInvalidation dataInvalidation)
    {
        _navigation = navigation;
        _assetDetailsService = assetDetailsService;

        Timeframes = new ObservableCollection<string> { "1ч", "1д", "7д", "30д", "1г" };
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
        _reloadCommand = new DelegateCommand(_ => _ = LoadByRouteAsync(_navigation.Current));

        _navigation.RouteChanged += route =>
        {
            if (string.Equals(route.Key, AppRoutes.AssetDetails, StringComparison.OrdinalIgnoreCase))
            {
                _ = LoadByRouteAsync(route);
            }
        };

        dataInvalidation.DataInvalidated += (_, _) =>
        {
            if (string.Equals(_navigation.Current.Key, AppRoutes.AssetDetails, StringComparison.OrdinalIgnoreCase))
            {
                _ = LoadByRouteAsync(_navigation.Current);
            }
        };
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

    public ICommand ReloadCommand => _reloadCommand;

    public string AssetName { get; private set; } = "Актив";

    public string AssetTicker { get; private set; } = "---";

    public string LogoText { get; private set; } = "A";

    public string PriceText { get; private set; } = "Нет данных";

    public string DeltaText { get; private set; } = "Недоступно";

    public bool IsDeltaPositive { get; private set; }

    public string AssetCurrency { get; private set; } = "USD";

    public string MarketCapText { get; private set; } = "—";

    public string FdvText { get; private set; } = "—";

    public string PeText { get; private set; } = "—";

    public string Volume24hText { get; private set; } = "—";

    public string SupplyText { get; private set; } = "—";

    public string MarketDataSource { get; private set; } = "Источник: PostgreSQL";

    public string SelectedTimeframe
    {
        get => _selectedTimeframe;
        set
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "1д" : value.Trim();

            if (SetProperty(ref _selectedTimeframe, normalized))
            {
                _ = LoadByRouteAsync(_navigation.Current);
            }
        }
    }

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

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshStateProperties();
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
                RefreshStateProperties();
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
                RefreshStateProperties();
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

    public bool HasBasicMetrics => HasData && BasicMetrics.Count > 0;

    public bool HasRiskMetrics => HasData && RiskMetrics.Count > 0;

    public static AssetDetailsViewModel CreateDesignData()
    {
        AppNavigationService navigation = new();
        RuntimeDataInvalidation invalidation = new();
        AssetDetailsViewModel viewModel = new(navigation, new DesignAssetDetailsService(), invalidation);

        navigation.Register(new AppRoute(AppRoutes.AssetDetails, "Детальная информация", "Все активы / Apple Inc."));
        navigation.Navigate(
            AppRoutes.AssetDetails,
            new Dictionary<string, string>
            {
                ["assetId"] = DesignAssetDetailsService.PrimaryAssetId.ToString()
            },
            "Apple Inc.",
            "Все активы / Apple Inc.");

        return viewModel;
    }

    private async Task LoadByRouteAsync(AppRoute route)
    {
        if (!string.Equals(route.Key, AppRoutes.AssetDetails, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

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
                ClearData();
                IsNotFound = true;
                return;
            }

            AssetDetailsReadModel? model = await _assetDetailsService
                .GetAsync(assetId, SelectedTimeframe)
                .ConfigureAwait(true);

            if (model is null)
            {
                ClearData();
                IsNotFound = true;
                return;
            }

            ApplyModel(model);
        }
        catch (Exception ex)
        {
            ClearData();
            HasError = true;
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyModel(AssetDetailsReadModel model)
    {
        AssetName = model.AssetName;
        AssetTicker = model.AssetTicker;
        LogoText = model.LogoText;
        PriceText = model.PriceText;
        DeltaText = model.DeltaText;
        IsDeltaPositive = model.IsDeltaPositive;
        AssetCurrency = model.CurrencyCode;
        MarketCapText = model.MarketCapText;
        FdvText = model.FdvText;
        PeText = model.PeText;
        Volume24hText = model.Volume24hText;
        SupplyText = model.SupplyText;
        MarketDataSource = model.MarketDataSource;

        Candles.Clear();
        foreach (AssetDetailsCandle candle in model.Candles)
        {
            Candles.Add(new CandlestickPointViewModel(
                candle.Timestamp,
                candle.Open,
                candle.High,
                candle.Low,
                candle.Close,
                candle.Volume));
        }

        ResetCandlesVisibleRange();

        BasicMetrics.Clear();
        foreach (AssetDetailsMetric metric in model.BasicMetrics)
        {
            BasicMetrics.Add(AssetMetricItemViewModel.FromMetric(metric));
        }

        RiskMetrics.Clear();
        foreach (AssetDetailsMetric metric in model.RiskMetrics)
        {
            RiskMetrics.Add(AssetMetricItemViewModel.FromMetric(metric));
        }

        _allTransactions = model.Transactions
            .Select(AssetTransactionRowViewModel.FromTransaction)
            .ToArray();

        ApplyTransactionFilters();

        OnPropertyChanged(nameof(AssetName));
        OnPropertyChanged(nameof(AssetTicker));
        OnPropertyChanged(nameof(LogoText));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(DeltaText));
        OnPropertyChanged(nameof(IsDeltaPositive));
        OnPropertyChanged(nameof(AssetCurrency));
        OnPropertyChanged(nameof(MarketCapText));
        OnPropertyChanged(nameof(FdvText));
        OnPropertyChanged(nameof(PeText));
        OnPropertyChanged(nameof(Volume24hText));
        OnPropertyChanged(nameof(SupplyText));
        OnPropertyChanged(nameof(MarketDataSource));
        RefreshStateProperties();
    }

    private void ClearData()
    {
        Candles.Clear();
        BasicMetrics.Clear();
        RiskMetrics.Clear();
        VisibleTransactions.Clear();
        _allTransactions = [];

        AssetName = "Актив";
        AssetTicker = "---";
        LogoText = "A";
        PriceText = "Нет данных";
        DeltaText = "Недоступно";
        IsDeltaPositive = false;
        AssetCurrency = "USD";
        MarketCapText = "—";
        FdvText = "—";
        PeText = "—";
        Volume24hText = "—";
        SupplyText = "—";
        MarketDataSource = "Источник: PostgreSQL";

        OnPropertyChanged(nameof(AssetName));
        OnPropertyChanged(nameof(AssetTicker));
        OnPropertyChanged(nameof(LogoText));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(DeltaText));
        OnPropertyChanged(nameof(IsDeltaPositive));
        OnPropertyChanged(nameof(AssetCurrency));
        OnPropertyChanged(nameof(MarketCapText));
        OnPropertyChanged(nameof(FdvText));
        OnPropertyChanged(nameof(PeText));
        OnPropertyChanged(nameof(Volume24hText));
        OnPropertyChanged(nameof(SupplyText));
        OnPropertyChanged(nameof(MarketDataSource));
        RefreshStateProperties();
    }

    private void ResetCandlesVisibleRange()
    {
        if (Candles.Count == 0)
        {
            CandlesVisibleStartIndex = 0;
            CandlesVisibleEndIndex = 0;
            return;
        }

        CandlesVisibleStartIndex = Math.Max(0, Candles.Count - 48);
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
                || item.DateText.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.PriceText.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.QuantityText.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.AmountText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = _sortKey switch
        {
            "type" => _sortDescending
                ? query.OrderByDescending(item => item.TypeLabel, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(item => item.TypeLabel, StringComparer.OrdinalIgnoreCase),

            "price" => _sortDescending
                ? query.OrderByDescending(item => item.Price)
                : query.OrderBy(item => item.Price),

            "quantity" => _sortDescending
                ? query.OrderByDescending(item => item.Quantity)
                : query.OrderBy(item => item.Quantity),

            "amount" => _sortDescending
                ? query.OrderByDescending(item => item.Amount)
                : query.OrderBy(item => item.Amount),

            _ => _sortDescending
                ? query.OrderByDescending(item => item.Date)
                : query.OrderBy(item => item.Date)
        };

        VisibleTransactions.Clear();

        foreach (AssetTransactionRowViewModel row in query)
        {
            VisibleTransactions.Add(row);
        }

        RefreshStateProperties();
    }

    private void RefreshStateProperties()
    {
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(ShowEmptyChart));
        OnPropertyChanged(nameof(ShowTransactions));
        OnPropertyChanged(nameof(IsTransactionsEmpty));
        OnPropertyChanged(nameof(HasBasicMetrics));
        OnPropertyChanged(nameof(HasRiskMetrics));
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

    private sealed class DesignAssetDetailsService : IAssetDetailsService
    {
        public static readonly Guid PrimaryAssetId = Guid.Parse("0a896663-ec39-4ebc-9b28-bf537f8f2fe0");

        public Task<AssetDetailsReadModel?> GetAsync(
            Guid assetId,
            string timeframe,
            CancellationToken cancellationToken = default)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow.AddDays(-10);

            AssetDetailsCandle[] candles = Enumerable.Range(0, 48)
                .Select(index =>
                {
                    decimal basePrice = 180m + index * 0.18m + (decimal)Math.Sin(index * 0.65d) * 2.1m;
                    decimal open = basePrice;
                    decimal close = basePrice + (index % 2 == 0 ? 1.15m : -0.82m);
                    decimal high = Math.Max(open, close) + 1.7m;
                    decimal low = Math.Min(open, close) - 1.3m;

                    return new AssetDetailsCandle(
                        start.AddHours(index * 5),
                        open,
                        high,
                        low,
                        close,
                        25_000m + index * 900m);
                })
                .ToArray();

            AssetDetailsMetric[] basic =
            [
                new("SMA 50/200", "Золотой крест", "Цена выше долгосрочной средней", AssetDetailsMetricSeverity.Good),
                new("RSI", "57.8", "Нейтральная зона", AssetDetailsMetricSeverity.Good),
                new("ATR", "3.42", "Умеренная волатильность", AssetDetailsMetricSeverity.Neutral),
                new("Turnover", "7.4%", "Здоровая ликвидность", AssetDetailsMetricSeverity.Good)
            ];

            AssetDetailsMetric[] risk =
            [
                new("Max Drawdown", "18.3%", "Максимальная историческая просадка", AssetDetailsMetricSeverity.Warning),
                new("VaR 95%", "3.9%", "Историческая оценка дневного риска", AssetDetailsMetricSeverity.Warning),
                new("CVaR", "5.8%", "Средний хвостовой убыток", AssetDetailsMetricSeverity.Warning),
                new("Sharpe", "1.21", "Доходность на единицу риска", AssetDetailsMetricSeverity.Good),
                new("Sortino", "1.47", "Доходность на негативную волатильность", AssetDetailsMetricSeverity.Good),
                new("Calmar", "0.93", "Доходность к просадке", AssetDetailsMetricSeverity.Neutral),
                new("Hurst", "0.58", "Трендовость ряда", AssetDetailsMetricSeverity.Good),
                new("Z-Score", "+1.07", "Отклонение от средней", AssetDetailsMetricSeverity.Neutral),
                new("Beta", "1.12", "Чувствительность к рынку", AssetDetailsMetricSeverity.Neutral),
                new("Spread", "0.12%", "Оценка торгового спреда", AssetDetailsMetricSeverity.Good)
            ];

            AssetDetailsTransaction[] transactions =
            [
                new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2), "Покупка", 186.20m, 2m, 372.40m, 0m, "USD", "Completed"),
                new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-5), "Покупка", 184.30m, 1.5m, 276.45m, 0m, "USD", "Completed"),
                new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-10), "Продажа", 188.10m, 0.7m, 131.67m, 0m, "USD", "Completed")
            ];

            AssetDetailsReadModel model = new(
                PrimaryAssetId,
                "Apple Inc.",
                "AAPL",
                "AP",
                "$186.43",
                "↗ +0.84%",
                true,
                "USD",
                "$2.89T",
                "$2.94T",
                "28.4",
                "$81.2B",
                "15.6B AAPL",
                "Источник: Finnhub + PostgreSQL",
                candles,
                basic,
                risk,
                transactions);

            return Task.FromResult<AssetDetailsReadModel?>(model);
        }
    }
}

public sealed record AssetMetricItemViewModel(
    string Label,
    string Value,
    string Hint,
    string Severity)
{
    public bool IsGood => string.Equals(Severity, "good", StringComparison.OrdinalIgnoreCase);

    public bool IsWarning => string.Equals(Severity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsDanger => string.Equals(Severity, "danger", StringComparison.OrdinalIgnoreCase);

    public static AssetMetricItemViewModel FromMetric(AssetDetailsMetric metric)
    {
        string severity = metric.Severity switch
        {
            AssetDetailsMetricSeverity.Good => "good",
            AssetDetailsMetricSeverity.Warning => "warning",
            AssetDetailsMetricSeverity.Danger => "danger",
            _ => "neutral"
        };

        return new AssetMetricItemViewModel(metric.Label, metric.Value, metric.Hint, severity);
    }
}

public sealed record AssetTransactionRowViewModel(
    Guid TransactionId,
    DateTimeOffset Date,
    string TypeLabel,
    decimal Price,
    decimal Quantity,
    decimal Amount,
    decimal FeeAmount,
    string Currency,
    string Status)
{
    public string DateText => Date.LocalDateTime.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public string PriceText => $"{Price:N2} {Currency}";

    public string QuantityText => $"{Quantity:N4}";

    public string AmountText => $"{Amount:N2} {Currency}";

    public string FeeText => $"{FeeAmount:N2} {Currency}";

    public bool IsBuy => TypeLabel.Contains("покуп", StringComparison.OrdinalIgnoreCase)
        || TypeLabel.Contains("buy", StringComparison.OrdinalIgnoreCase);

    public bool IsSell => TypeLabel.Contains("прод", StringComparison.OrdinalIgnoreCase)
        || TypeLabel.Contains("sell", StringComparison.OrdinalIgnoreCase);

    public static AssetTransactionRowViewModel FromTransaction(AssetDetailsTransaction transaction)
    {
        return new AssetTransactionRowViewModel(
            transaction.TransactionId,
            transaction.Date,
            transaction.TypeLabel,
            transaction.Price,
            transaction.Quantity,
            transaction.Amount,
            transaction.FeeAmount,
            transaction.Currency,
            transaction.Status);
    }
}
