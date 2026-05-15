using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.Core.Application.AssetDetails;
using Proxima.Core.Domain.Assets;

namespace Proxima.App.Views.AssetDetails;

public sealed class AssetDetailsViewModel : ViewModelBase
{
    private const string DefaultTimeframe = "1д";

    private readonly IAppNavigationService _navigation;
    private readonly IAssetDetailsService _assetDetailsService;

    private readonly DelegateCommand _sortByDateCommand;
    private readonly DelegateCommand _sortByTypeCommand;
    private readonly DelegateCommand _sortByPriceCommand;
    private readonly DelegateCommand _sortByQuantityCommand;
    private readonly DelegateCommand _sortByAmountCommand;
    private readonly DelegateCommand _sortByStatusCommand;
    private readonly DelegateCommand _resetChartZoomCommand;
    private readonly DelegateCommand _reloadCommand;
    private readonly DelegateCommand _selectTimeframeCommand;
    private readonly DelegateCommand _startTransactionCommand;

    private IReadOnlyList<AssetTransactionRowViewModel> _allTransactions = [];
    private string _searchQuery = string.Empty;
    private string _sortKey = "date";
    private bool _sortDescending = true;
    private bool _isLoading;
    private bool _isNotFound;
    private bool _hasError;
    private string _errorText = string.Empty;
    private string _selectedTimeframe = DefaultTimeframe;
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
        TimeframeOptions = new ObservableCollection<TimeframeOptionViewModel>(
            Timeframes.Select(item => new TimeframeOptionViewModel(item, string.Equals(item, DefaultTimeframe, StringComparison.OrdinalIgnoreCase))));

        Candles = new ObservableCollection<CandlestickPointViewModel>();
        BasicMetrics = new ObservableCollection<AssetMetricItemViewModel>();
        RiskMetrics = new ObservableCollection<AssetMetricItemViewModel>();
        TechnicalIndicators = new ObservableCollection<AssetMetricItemViewModel>();
        RiskManagementMetrics = new ObservableCollection<AssetMetricItemViewModel>();
        ContextMetrics = new ObservableCollection<AssetMetricItemViewModel>();
        MarketDynamicsMetrics = new ObservableCollection<AssetMetricItemViewModel>();
        VisibleTransactions = new ObservableCollection<AssetTransactionRowViewModel>();

        _sortByDateCommand = new DelegateCommand(_ => SortBy("date"));
        _sortByTypeCommand = new DelegateCommand(_ => SortBy("type"));
        _sortByPriceCommand = new DelegateCommand(_ => SortBy("price"));
        _sortByQuantityCommand = new DelegateCommand(_ => SortBy("quantity"));
        _sortByAmountCommand = new DelegateCommand(_ => SortBy("amount"));
        _sortByStatusCommand = new DelegateCommand(_ => SortBy("status"));
        _resetChartZoomCommand = new DelegateCommand(_ => ResetCandlesVisibleRange());
        _reloadCommand = new DelegateCommand(_ => _ = LoadByRouteAsync(_navigation.Current));
        _selectTimeframeCommand = new DelegateCommand(SelectTimeframe);
        _startTransactionCommand = new DelegateCommand(_ => NavigateToManualTransaction());

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

    public ObservableCollection<TimeframeOptionViewModel> TimeframeOptions { get; }

    public ObservableCollection<CandlestickPointViewModel> Candles { get; }

    public ObservableCollection<AssetMetricItemViewModel> BasicMetrics { get; }

    public ObservableCollection<AssetMetricItemViewModel> RiskMetrics { get; }

    public ObservableCollection<AssetMetricItemViewModel> TechnicalIndicators { get; }

    public ObservableCollection<AssetMetricItemViewModel> RiskManagementMetrics { get; }

    public ObservableCollection<AssetMetricItemViewModel> ContextMetrics { get; }

    public ObservableCollection<AssetMetricItemViewModel> MarketDynamicsMetrics { get; }

    public ObservableCollection<AssetTransactionRowViewModel> VisibleTransactions { get; }

    public ICommand SortByDateCommand => _sortByDateCommand;

    public ICommand SortByTypeCommand => _sortByTypeCommand;

    public ICommand SortByPriceCommand => _sortByPriceCommand;

    public ICommand SortByQuantityCommand => _sortByQuantityCommand;

    public ICommand SortByAmountCommand => _sortByAmountCommand;

    public ICommand SortByStatusCommand => _sortByStatusCommand;

    public ICommand ResetChartZoomCommand => _resetChartZoomCommand;

    public ICommand ReloadCommand => _reloadCommand;

    public ICommand SelectTimeframeCommand => _selectTimeframeCommand;

    public ICommand StartTransactionCommand => _startTransactionCommand;

    public string AssetName { get; private set; } = "Актив";

    public string AssetTicker { get; private set; } = "---";

    public string AssetTickerMutedText => $"({AssetTicker})";

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

    public string BlackSwanDropText { get; private set; } = "—";

    public string VarText { get; private set; } = "—";

    public string CvarText { get; private set; } = "—";

    public string SharpeText { get; private set; } = "—";

    public string SortinoText { get; private set; } = "—";

    public string CalmarText { get; private set; } = "—";

    public string HurstText { get; private set; } = "—";

    public string ZScoreText { get; private set; } = "—";

    public string CorrelationText { get; private set; } = "—";

    public string BetaText { get; private set; } = "—";

    public string SpreadText { get; private set; } = "—";

    public string HistoricalVolatilityText { get; private set; } = "—";

    public string ImpliedVolatilityText { get; private set; } = "—";

    public string SkewnessText { get; private set; } = "—";

    public string KurtosisText { get; private set; } = "—";

    public string DepthText { get; private set; } = "—";

    public string SelectedTimeframe
    {
        get => _selectedTimeframe;
        set
        {
            string normalized = NormalizeTimeframe(value);

            if (SetProperty(ref _selectedTimeframe, normalized))
            {
                UpdateTimeframeSelection();
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

        navigation.Register(new AppRoute(AppRoutes.AssetDetails, "Детальная информация", "Все активы / NVIDIA"));
        navigation.Register(new AppRoute(AppRoutes.ManualImport, "Ручной импорт", "Все активы / Ручной импорт"));
        navigation.Navigate(
            AppRoutes.AssetDetails,
            new Dictionary<string, string>
            {
                ["assetId"] = DesignAssetDetailsService.PrimaryAssetId.ToString()
            },
            "NVIDIA",
            "Все активы / Основной портфель / NVIDIA");

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

        RebuildMetricHighlights();

        _allTransactions = model.Transactions
            .Select(transaction => AssetTransactionRowViewModel.FromTransaction(transaction, model.AssetType, model.AssetTicker))
            .ToArray();

        ApplyTransactionFilters();
        NotifyAssetPropertiesChanged();
        RefreshStateProperties();
    }

    private void RebuildMetricHighlights()
    {
        TechnicalIndicators.Clear();
        RiskManagementMetrics.Clear();
        ContextMetrics.Clear();
        MarketDynamicsMetrics.Clear();

        AssetMetricItemViewModel sma = FindMetric(BasicMetrics, "SMA", "Скольз")
            ?? new AssetMetricItemViewModel("SMA 50/200", "—", "Тренд по скользящим средним", "neutral");
        AssetMetricItemViewModel rsi = FindMetric(BasicMetrics, "RSI")
            ?? new AssetMetricItemViewModel("RSI", "—", "Перекупленность / перепроданность", "neutral");
        AssetMetricItemViewModel atr = FindMetric(BasicMetrics, "ATR")
            ?? new AssetMetricItemViewModel("ATR", "—", "Средний истинный диапазон", "neutral");
        AssetMetricItemViewModel turnover = FindMetric(BasicMetrics, "Turnover", "Оборач")
            ?? new AssetMetricItemViewModel("Turnover", "—", "Объем торгов / капитализация", "neutral");

        TechnicalIndicators.Add(sma);
        TechnicalIndicators.Add(rsi);
        TechnicalIndicators.Add(atr);
        TechnicalIndicators.Add(turnover);

        AssetMetricItemViewModel maxDrawdown = FindMetric(RiskMetrics, "Max Drawdown", "MDD")
            ?? new AssetMetricItemViewModel("Max Drawdown", "—", "Максимальная историческая просадка", "warning");
        AssetMetricItemViewModel var = FindMetric(RiskMetrics, "VaR")
            ?? new AssetMetricItemViewModel("VaR 95%", "—", "Ожидаемый дневной убыток", "warning");
        AssetMetricItemViewModel cvar = FindMetric(RiskMetrics, "CVaR")
            ?? new AssetMetricItemViewModel("CVaR", "—", "Ожидаемый хвостовой убыток", "warning");
        AssetMetricItemViewModel sharpe = FindMetric(RiskMetrics, "Sharpe", "Шарп")
            ?? new AssetMetricItemViewModel("Sharpe", "—", "Доходность на единицу риска", "neutral");
        AssetMetricItemViewModel sortino = FindMetric(RiskMetrics, "Sortino", "Сортино")
            ?? new AssetMetricItemViewModel("Sortino", "—", "Доходность на негативную волатильность", "neutral");
        AssetMetricItemViewModel calmar = FindMetric(RiskMetrics, "Calmar", "Кальмар")
            ?? new AssetMetricItemViewModel("Calmar", "—", "Доходность относительно максимальной просадки", "neutral");
        AssetMetricItemViewModel hurst = FindMetric(RiskMetrics, "Hurst", "Херст")
            ?? new AssetMetricItemViewModel("Hurst", "—", "Оценка трендовости актива", "neutral");
        AssetMetricItemViewModel zScore = FindMetric(RiskMetrics, "Z-Score", "Z Score")
            ?? new AssetMetricItemViewModel("Z-Score", "—", "Отклонение цены от средней", "neutral");
        AssetMetricItemViewModel beta = FindMetric(RiskMetrics, "Beta", "Бета")
            ?? new AssetMetricItemViewModel("Beta", "—", "Чувствительность к рынку", "neutral");
        AssetMetricItemViewModel spread = FindMetric(RiskMetrics, "Spread", "Спред")
            ?? new AssetMetricItemViewModel("Spread", "—", "Оценка торгового спреда", "neutral");

        BlackSwanDropText = ToNegativePercentText(maxDrawdown.Value);
        VarText = var.Value;
        CvarText = cvar.Value;
        SharpeText = sharpe.Value;
        SortinoText = sortino.Value;
        CalmarText = calmar.Value;
        HurstText = hurst.Value;
        ZScoreText = zScore.Value;
        BetaText = beta.Value;
        SpreadText = spread.Value;

        RiskManagementMetrics.Add(maxDrawdown);
        RiskManagementMetrics.Add(var);
        RiskManagementMetrics.Add(cvar);
        RiskManagementMetrics.Add(new AssetMetricItemViewModel("Skew", SkewnessText, "Асимметрия распределения", "neutral"));
        RiskManagementMetrics.Add(new AssetMetricItemViewModel("Kurtosis", KurtosisText, "Толстые хвосты риска", "neutral"));

        ContextMetrics.Add(hurst);
        ContextMetrics.Add(zScore);
        ContextMetrics.Add(new AssetMetricItemViewModel("Корреляция", CorrelationText, "Связь с базовым рынком", "neutral"));

        MarketDynamicsMetrics.Add(new AssetMetricItemViewModel("Оборот", turnover.Value, turnover.Hint, turnover.Severity));
        MarketDynamicsMetrics.Add(spread);
        MarketDynamicsMetrics.Add(beta);
        MarketDynamicsMetrics.Add(new AssetMetricItemViewModel("HV", HistoricalVolatilityText, "Историческая волатильность", "neutral"));
        MarketDynamicsMetrics.Add(new AssetMetricItemViewModel("IV", ImpliedVolatilityText, "Ожидаемая волатильность", "neutral"));
        MarketDynamicsMetrics.Add(new AssetMetricItemViewModel("Depth", DepthText, "Глубина рынка", "neutral"));

        NotifyMetricHighlightPropertiesChanged();
    }

    private void ClearData()
    {
        Candles.Clear();
        BasicMetrics.Clear();
        RiskMetrics.Clear();
        TechnicalIndicators.Clear();
        RiskManagementMetrics.Clear();
        ContextMetrics.Clear();
        MarketDynamicsMetrics.Clear();
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
        BlackSwanDropText = "—";
        VarText = "—";
        CvarText = "—";
        SharpeText = "—";
        SortinoText = "—";
        CalmarText = "—";
        HurstText = "—";
        ZScoreText = "—";
        CorrelationText = "—";
        BetaText = "—";
        SpreadText = "—";
        HistoricalVolatilityText = "—";
        ImpliedVolatilityText = "—";
        SkewnessText = "—";
        KurtosisText = "—";
        DepthText = "—";

        NotifyAssetPropertiesChanged();
        NotifyMetricHighlightPropertiesChanged();
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

        CandlesVisibleStartIndex = Math.Max(0, Candles.Count - 72);
        CandlesVisibleEndIndex = Candles.Count - 1;
    }

    private void SelectTimeframe(object? parameter)
    {
        string? value = parameter switch
        {
            TimeframeOptionViewModel option => option.Label,
            string text => text,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        SelectedTimeframe = value;
    }

    private void UpdateTimeframeSelection()
    {
        foreach (TimeframeOptionViewModel option in TimeframeOptions)
        {
            option.IsSelected = string.Equals(option.Label, SelectedTimeframe, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void NavigateToManualTransaction()
    {
        _navigation.Navigate(
            AppRoutes.ManualImport,
            titleOverride: "Новая транзакция",
            breadcrumbOverride: $"Все активы / {AssetName} / Новая транзакция");
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

            "status" => _sortDescending
                ? query.OrderByDescending(item => item.StatusText, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(item => item.StatusText, StringComparer.OrdinalIgnoreCase),

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

    private static AssetMetricItemViewModel? FindMetric(
        IEnumerable<AssetMetricItemViewModel> metrics,
        params string[] tokens)
    {
        return metrics.FirstOrDefault(metric => tokens.Any(token =>
            metric.Label.Contains(token, StringComparison.OrdinalIgnoreCase)
            || metric.Hint.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizeTimeframe(string? value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? DefaultTimeframe : value.Trim();
        return normalized switch
        {
            "1h" or "1H" => "1ч",
            "1d" or "1D" => "1д",
            "7d" or "7D" => "7д",
            "30d" or "30D" => "30д",
            "1y" or "1Y" => "1г",
            _ => normalized
        };
    }

    private static string ToNegativePercentText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "—")
        {
            return "—";
        }

        string trimmed = value.Trim();
        if (trimmed.StartsWith("-", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return $"-{trimmed}";
    }

    private void NotifyAssetPropertiesChanged()
    {
        OnPropertyChanged(nameof(AssetName));
        OnPropertyChanged(nameof(AssetTicker));
        OnPropertyChanged(nameof(AssetTickerMutedText));
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
    }

    private void NotifyMetricHighlightPropertiesChanged()
    {
        OnPropertyChanged(nameof(BlackSwanDropText));
        OnPropertyChanged(nameof(VarText));
        OnPropertyChanged(nameof(CvarText));
        OnPropertyChanged(nameof(SharpeText));
        OnPropertyChanged(nameof(SortinoText));
        OnPropertyChanged(nameof(CalmarText));
        OnPropertyChanged(nameof(HurstText));
        OnPropertyChanged(nameof(ZScoreText));
        OnPropertyChanged(nameof(CorrelationText));
        OnPropertyChanged(nameof(BetaText));
        OnPropertyChanged(nameof(SpreadText));
        OnPropertyChanged(nameof(HistoricalVolatilityText));
        OnPropertyChanged(nameof(ImpliedVolatilityText));
        OnPropertyChanged(nameof(SkewnessText));
        OnPropertyChanged(nameof(KurtosisText));
        OnPropertyChanged(nameof(DepthText));
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
            DateTimeOffset start = DateTimeOffset.UtcNow.AddDays(-45);

            AssetDetailsCandle[] candles = Enumerable.Range(0, 96)
                .Select(index =>
                {
                    decimal basePrice = 760m + index * 1.05m + (decimal)Math.Sin(index * 0.42d) * 18.5m;
                    decimal open = basePrice;
                    decimal close = basePrice + (index % 3 == 0 ? 13.6m : -8.2m);
                    decimal high = Math.Max(open, close) + 15.7m;
                    decimal low = Math.Min(open, close) - 13.3m;

                    return new AssetDetailsCandle(
                        start.AddHours(index * 8),
                        open,
                        high,
                        low,
                        close,
                        25_000_000m + index * 900_000m);
                })
                .ToArray();

            AssetDetailsMetric[] basic =
            [
                new("SMA 50/200", "Золотой крест", "Тренд по скользящим средним", AssetDetailsMetricSeverity.Good),
                new("RSI", "68.2", "Перекупленность", AssetDetailsMetricSeverity.Warning),
                new("ATR", "24.15", "Высокая волатильность", AssetDetailsMetricSeverity.Neutral),
                new("Turnover", "2.4%", "Объем торгов / капитализация", AssetDetailsMetricSeverity.Neutral)
            ];

            AssetDetailsMetric[] risk =
            [
                new("Max Drawdown", "14.2%", "Максимальная историческая просадка", AssetDetailsMetricSeverity.Warning),
                new("VaR 95%", "3.14%", "Ожидаемый дневной убыток в нормальных условиях", AssetDetailsMetricSeverity.Warning),
                new("CVaR", "4.82%", "Ожидаемый убыток в хвостовых сценариях", AssetDetailsMetricSeverity.Warning),
                new("Sharpe", "2.84", "Доходность на единицу риска", AssetDetailsMetricSeverity.Good),
                new("Sortino", "3.12", "Доходность на негативную волатильность", AssetDetailsMetricSeverity.Good),
                new("Calmar", "1.45", "Доходность относительно максимальной просадки", AssetDetailsMetricSeverity.Good),
                new("Hurst", "0.68", "Оценка трендовости актива", AssetDetailsMetricSeverity.Good),
                new("Z-Score", "+2.14", "Отклонение цены от средней", AssetDetailsMetricSeverity.Warning),
                new("Beta", "1.68", "Чувствительность к рынку", AssetDetailsMetricSeverity.Warning),
                new("Spread", "0.02%", "Оценка торгового спреда", AssetDetailsMetricSeverity.Good)
            ];

            AssetDetailsTransaction[] transactions =
            [
                new(Guid.NewGuid(), new DateTimeOffset(2026, 3, 26, 12, 0, 0, TimeSpan.Zero), "Покупка", 842.10m, 120m, 101_052m, 12.40m, "USD", "Completed"),
                new(Guid.NewGuid(), new DateTimeOffset(2026, 3, 25, 12, 0, 0, TimeSpan.Zero), "Покупка", 815.45m, 50m, 40_772.50m, 8.20m, "USD", "Completed"),
                new(Guid.NewGuid(), new DateTimeOffset(2026, 3, 24, 12, 0, 0, TimeSpan.Zero), "Продажа", 732.18m, 45m, 32_948.10m, 14.50m, "USD", "Completed")
            ];

            AssetDetailsReadModel model = new(
                PrimaryAssetId,
                "NVIDIA Corp.",
                "NVDA",
                AssetType.Stock,
                "NV",
                "$875.28",
                "↗ +4.21%",
                true,
                "USD",
                "$2.16T",
                "$2.21T",
                "74.2",
                "$42.8B",
                "2.47B NVDA",
                "Источник: Twelve Data + PostgreSQL",
                candles,
                basic,
                risk,
                transactions);

            return Task.FromResult<AssetDetailsReadModel?>(model);
        }
    }
}

public sealed class TimeframeOptionViewModel(string label, bool isSelected) : ViewModelBase
{
    private bool _isSelected = isSelected;

    public string Label { get; } = label;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
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
    string Status,
    AssetType AssetType,
    string AssetTicker)
{
    public string DateText => Date.LocalDateTime.ToString("dd MMM, yyyy", CultureInfo.GetCultureInfo("ru-RU"));

    public string PriceText => $"${Price:N2}";

    public string QuantityText => AssetType switch
    {
        AssetType.Crypto => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N4} {NormalizedAssetTicker}"),
        AssetType.Stock or AssetType.Etf => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} Shares"),
        AssetType.Bond => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} Units"),
        AssetType.Cash or AssetType.Currency => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2} {CashQuantitySymbol}"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{Quantity:N2}"),
    };

    public string AmountText => $"${Amount:N2}";

    public string FeeText => $"${FeeAmount:N2}";

    public string StatusText => string.IsNullOrWhiteSpace(Status) ? "Completed" : Status;

    public bool IsBuy => TypeLabel.Contains("покуп", StringComparison.OrdinalIgnoreCase)
        || TypeLabel.Contains("buy", StringComparison.OrdinalIgnoreCase);

    public bool IsSell => TypeLabel.Contains("прод", StringComparison.OrdinalIgnoreCase)
        || TypeLabel.Contains("sell", StringComparison.OrdinalIgnoreCase);

    private string NormalizedAssetTicker
    {
        get
        {
            string ticker = AssetTicker.Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(ticker) ? Currency : ticker;
        }
    }

    private string CashQuantitySymbol
    {
        get
        {
            string ticker = NormalizedAssetTicker;
            int slash = ticker.IndexOf('/', StringComparison.Ordinal);
            if (slash > 0)
            {
                return ticker[..slash];
            }

            return string.IsNullOrWhiteSpace(ticker) ? Currency : ticker;
        }
    }

    public static AssetTransactionRowViewModel FromTransaction(
        AssetDetailsTransaction transaction,
        AssetType assetType,
        string assetTicker)
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
            transaction.Status,
            assetType,
            assetTicker);
    }
}
