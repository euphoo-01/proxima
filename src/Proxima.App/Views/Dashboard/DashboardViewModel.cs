using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Media;
using Proxima.Analytics.Dashboard;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.ViewModels;

namespace Proxima.App.Views.Dashboard;

public sealed class DashboardViewModel : ViewModelBase
{
    private static readonly string[] AllocationPalette = ["#003658", "#1D7329", "#B7D5E4", "#A67C00", "#7158E2", "#7D8793"];

    private readonly IDashboardDataProvider _dataProvider;
    private readonly IShellState _shellState;
    private readonly IAppNavigationService _navigation;
    private readonly DelegateCommand _selectTimeframeCommand;
    private readonly DelegateCommand _rowMoreCommand;
    private readonly DelegateCommand _moreTransactionsCommand;
    private IReadOnlyList<DashboardTransactionRowViewModel> _allTransactions = [];
    private DashboardSnapshot _snapshot = DashboardSnapshot.Empty("USD");
    private string _searchQuery = string.Empty;
    private string _selectedTimeframe = "1д";

    public DashboardViewModel(
        IDashboardDataProvider dataProvider,
        IShellState shellState,
        IRuntimeDataInvalidation dataInvalidation,
        IAppNavigationService navigation)
    {
        _dataProvider = dataProvider;
        _shellState = shellState;
        _navigation = navigation;

        _selectTimeframeCommand = new DelegateCommand(SelectTimeframe);
        _rowMoreCommand = new DelegateCommand(_ => { });
        _moreTransactionsCommand = new DelegateCommand(_ => _navigation.Navigate(AppRoutes.Assets));

        Timeframes =
        [
            new DashboardTimeframeViewModel("1д", true),
            new DashboardTimeframeViewModel("7д", false),
            new DashboardTimeframeViewModel("1мес", false)
        ];

        Bars = [];
        AllocationRows = [];
        VisibleTransactions = [];

        _shellState.PortfolioChanged += (_, _) => Load();
        dataInvalidation.DataInvalidated += (_, _) => Load();

        Load();
    }

    public ObservableCollection<DashboardTimeframeViewModel> Timeframes { get; }

    public ObservableCollection<DashboardBarViewModel> Bars { get; }

    public ObservableCollection<DashboardAllocationRowViewModel> AllocationRows { get; }

    public ObservableCollection<DashboardTransactionRowViewModel> VisibleTransactions { get; }

    public IReadOnlyList<decimal> AllocationValues => AllocationRows.Select(static row => row.Value).ToArray();

    public string TotalPortfolioValueText { get; private set; } = "$0.00";

    public string GrowthPillText { get; private set; } = "↔ 0.00% (24ч)";

    public string MonthlyGrowthText { get; private set; } = "$0.00";

    public string RiskLevelText { get; private set; } = "Средний";

    public string BalanceBlockTitle { get; private set; } = "Нет данных";

    public string BalanceBlockSubtitle { get; private set; } = "Добавьте активы или импортируйте транзакции";

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyTransactionFilter();
            }
        }
    }

    public string SelectedTimeframe
    {
        get => _selectedTimeframe;
        set
        {
            string normalized = NormalizeTimeframe(value);

            if (SetProperty(ref _selectedTimeframe, normalized))
            {
                foreach (DashboardTimeframeViewModel item in Timeframes)
                {
                    item.IsSelected = string.Equals(item.Label, normalized, StringComparison.OrdinalIgnoreCase);
                }

                BuildBars(_snapshot);
            }
        }
    }

    public ICommand SelectTimeframeCommand => _selectTimeframeCommand;

    public ICommand RowMoreCommand => _rowMoreCommand;

    public ICommand MoreTransactionsCommand => _moreTransactionsCommand;

    public static DashboardViewModel CreateDesignData()
    {
        return new DashboardViewModel(
            new DesignDashboardDataProvider(),
            new MockShellState(),
            new RuntimeDataInvalidation(),
            new DesignNavigationService());
    }

    private void Load()
    {
        try
        {
            _snapshot = _dataProvider.GetSnapshot();
        }
        catch
        {
            _snapshot = DashboardSnapshot.Empty("USD");
        }

        decimal total = PortfolioDashboardCalculator.CalculateTotalValue(_snapshot.Assets);
        TotalPortfolioValueText = FormatMoney(total, _snapshot.Currency);

        Delta24h delta = PortfolioDashboardCalculator.Calculate24hDelta(_snapshot.Assets, _snapshot.PreviousQuotes);
        GrowthPillText = FormatDelta(delta);
        MonthlyGrowthText = FormatMoney(CalculatePeriodGrowth(_snapshot.FallbackSeries), _snapshot.Currency, showPlus: true);

        BuildBars(_snapshot);
        BuildAllocation(_snapshot);
        BuildTransactions(_snapshot);
        ApplyTransactionFilter();
        BuildRiskAndBalanceSummary(_snapshot);

        OnPropertyChanged(nameof(TotalPortfolioValueText));
        OnPropertyChanged(nameof(GrowthPillText));
        OnPropertyChanged(nameof(MonthlyGrowthText));
        OnPropertyChanged(nameof(RiskLevelText));
        OnPropertyChanged(nameof(BalanceBlockTitle));
        OnPropertyChanged(nameof(BalanceBlockSubtitle));
        OnPropertyChanged(nameof(AllocationValues));
    }

    private void BuildBars(DashboardSnapshot snapshot)
    {
        Bars.Clear();

        IReadOnlyList<decimal> source = SliceSeries(snapshot.FallbackSeries, SelectedTimeframe);
        if (source.Count == 0)
        {
            return;
        }

        decimal max = source.Max();
        decimal min = source.Min();
        decimal range = max - min;
        if (range <= 0m)
        {
            range = max <= 0m ? 1m : max;
        }

        DateTimeOffset start = DateTimeOffset.UtcNow.Date.AddDays(-(source.Count - 1));
        for (int i = 0; i < source.Count; i++)
        {
            decimal value = source[i];
            double normalized = (double)((value - min) / range);
            double height = 96 + normalized * 148;
            bool isActive = i == source.Count - 1;
            DateTimeOffset day = start.AddDays(i);

            Bars.Add(new DashboardBarViewModel(
                day.ToString("dd.MM", CultureInfo.InvariantCulture),
                height,
                isActive,
                isActive,
                FormatCompactMoney(value, snapshot.Currency)));
        }
    }

    private static IReadOnlyList<decimal> SliceSeries(IReadOnlyList<decimal> series, string timeframe)
    {
        if (series.Count == 0)
        {
            return [];
        }

        int take = NormalizeTimeframe(timeframe) switch
        {
            "1д" => Math.Min(10, series.Count),
            "7д" => Math.Min(14, series.Count),
            _ => Math.Min(30, series.Count)
        };

        return series.Skip(Math.Max(0, series.Count - take)).ToArray();
    }

    private void BuildAllocation(DashboardSnapshot snapshot)
    {
        AllocationRows.Clear();

        IReadOnlyList<AllocationSlice> slices = PortfolioDashboardCalculator.BuildAllocationByTag(snapshot.Assets)
            .Where(static slice => slice.Value > 0m)
            .OrderByDescending(static slice => slice.Value)
            .Take(6)
            .ToArray();

        decimal total = slices.Sum(static slice => slice.Value);
        if (total <= 0m)
        {
            OnPropertyChanged(nameof(AllocationValues));
            return;
        }

        for (int i = 0; i < slices.Count; i++)
        {
            AllocationSlice slice = slices[i];
            decimal percent = slice.Value / total * 100m;
            AllocationRows.Add(new DashboardAllocationRowViewModel(slice.Tag, slice.Value, percent, AllocationPalette[i % AllocationPalette.Length]));
        }

        OnPropertyChanged(nameof(AllocationValues));
    }

    private void BuildTransactions(DashboardSnapshot snapshot)
    {
        _allTransactions = snapshot.Transactions
            .OrderByDescending(static item => item.TradeDate)
            .Select(MapTransaction)
            .ToArray();
    }

    private void ApplyTransactionFilter()
    {
        IEnumerable<DashboardTransactionRowViewModel> query = _allTransactions;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string term = SearchQuery.Trim();
            query = query.Where(item =>
                item.AssetName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.TypeText.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.AmountText.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.DateText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        VisibleTransactions.Clear();

        foreach (DashboardTransactionRowViewModel item in query.Take(3))
        {
            VisibleTransactions.Add(item);
        }
    }

    private void BuildRiskAndBalanceSummary(DashboardSnapshot snapshot)
    {
        decimal total = snapshot.Assets.Sum(static asset => asset.Value);
        if (total <= 0m)
        {
            RiskLevelText = "Средний";
            BalanceBlockTitle = "Портфель пуст";
            BalanceBlockSubtitle = "Импортируйте активы, чтобы увидеть диверсификацию";
            return;
        }

        IReadOnlyList<AllocationSlice> allocation = PortfolioDashboardCalculator.BuildAllocationByTag(snapshot.Assets)
            .Where(static item => item.Value > 0m)
            .OrderByDescending(static item => item.Value)
            .ToArray();

        decimal topShare = allocation.Count == 0 ? 100m : allocation[0].Value / total * 100m;
        decimal cryptoShare = allocation
            .Where(static item => item.Tag.Contains("крип", StringComparison.OrdinalIgnoreCase) || item.Tag.Contains("crypto", StringComparison.OrdinalIgnoreCase))
            .Sum(static item => item.Value) / total * 100m;
        decimal drawdown = CalculateMaxDrawdown(snapshot.FallbackSeries);

        int score = 0;
        score += topShare switch
        {
            >= 80m => 3,
            >= 60m => 2,
            >= 40m => 1,
            _ => 0
        };
        score += cryptoShare switch
        {
            >= 60m => 3,
            >= 35m => 2,
            >= 15m => 1,
            _ => 0
        };
        score += drawdown switch
        {
            >= 35m => 3,
            >= 20m => 2,
            >= 10m => 1,
            _ => 0
        };
        score += snapshot.Assets.Count switch
        {
            <= 1 => 2,
            <= 2 => 1,
            _ => 0
        };

        RiskLevelText = score switch
        {
            <= 1 => "Низкий",
            <= 3 => "Средний",
            <= 5 => "Выше среднего",
            <= 7 => "Высокий",
            _ => "Очень высокий"
        };

        BalanceBlockTitle = $"{allocation.Count} категорий";
        BalanceBlockSubtitle = allocation.Count == 0
            ? "Недостаточно данных"
            : $"Крупнейшая доля: {allocation[0].Tag} — {topShare:0}%";
    }

    private static DashboardTransactionRowViewModel MapTransaction(DashboardTransactionSnapshot item)
    {
        bool positive = item.GrossAmount >= 0m;
        string iconKind = ResolveIconKind(item.AssetName, item.Ticker);
        return new DashboardTransactionRowViewModel(
            item.AssetName,
            FormatDate(item.TradeDate),
            item.TypeLabel,
            FormatMoney(item.GrossAmount, "USD", showPlus: true),
            iconKind,
            positive);
    }

    private void SelectTimeframe(object? parameter)
    {
        if (parameter is DashboardTimeframeViewModel selected)
        {
            SelectedTimeframe = selected.Label;
        }
    }

    private static decimal CalculatePeriodGrowth(IReadOnlyList<decimal> series)
    {
        if (series.Count < 2)
        {
            return 0m;
        }

        return series[^1] - series[0];
    }

    private static decimal CalculateMaxDrawdown(IReadOnlyList<decimal> series)
    {
        if (series.Count < 2)
        {
            return 0m;
        }

        decimal peak = series[0];
        decimal maxDrawdown = 0m;

        foreach (decimal value in series)
        {
            if (value > peak)
            {
                peak = value;
            }

            if (peak > 0m)
            {
                decimal drawdown = (peak - value) / peak * 100m;
                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }
        }

        return maxDrawdown;
    }

    private static string FormatDelta(Delta24h delta)
    {
        if (!delta.HasEnoughData || delta.Percent is null)
        {
            return "↔ 0.00% (24ч)";
        }

        string arrow = delta.Absolute >= 0m ? "↗" : "↘";
        string sign = delta.Absolute >= 0m ? "+" : "";
        return $"{arrow} {sign}{delta.Percent.Value:0.00}% (24ч)";
    }

    private static string FormatDate(DateTimeOffset date)
    {
        string month = date.Month switch
        {
            1 => "Янв",
            2 => "Фев",
            3 => "Март",
            4 => "Апр",
            5 => "Май",
            6 => "Июнь",
            7 => "Июль",
            8 => "Авг",
            9 => "Сен",
            10 => "Окт",
            11 => "Ноя",
            12 => "Дек",
            _ => date.ToString("MMM", CultureInfo.CurrentCulture)
        };

        return $"{month} {date.Day}, {date.Year}";
    }

    private static string ResolveIconKind(string assetName, string ticker)
    {
        string raw = $"{assetName} {ticker}";
        if (raw.Contains("btc", StringComparison.OrdinalIgnoreCase) || raw.Contains("bitcoin", StringComparison.OrdinalIgnoreCase))
        {
            return "bitcoin";
        }

        if (raw.Contains("s&p", StringComparison.OrdinalIgnoreCase) || raw.Contains("spy", StringComparison.OrdinalIgnoreCase))
        {
            return "sp";
        }

        return ticker.Length > 0 ? ticker[..Math.Min(2, ticker.Length)].ToUpperInvariant() : "A";
    }

    private static string FormatMoney(decimal value, string currency, bool showPlus = false)
    {
        string symbol = currency.ToUpperInvariant() switch
        {
            "USD" => "$",
            "BYN" => "Br ",
            "EUR" => "€",
            "BTC" => "₿",
            _ => currency.ToUpperInvariant() + " "
        };

        string sign = showPlus && value > 0m ? "+" : value < 0m ? "-" : string.Empty;
        decimal absolute = Math.Abs(value);
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{symbol}{absolute:N2}");
    }

    private static string FormatCompactMoney(decimal value, string currency)
    {
        string symbol = currency.ToUpperInvariant() == "USD" ? "$" : currency.ToUpperInvariant() + " ";
        decimal absolute = Math.Abs(value);
        if (absolute >= 1_000_000m)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{symbol}{absolute / 1_000_000m:0.##}M");
        }

        if (absolute >= 1_000m)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{symbol}{absolute / 1_000m:0.##}K");
        }

        return FormatMoney(value, currency);
    }

    private static string NormalizeTimeframe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "1д";
        }

        string normalized = value.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "1d" => "1д",
            "1д" => "1д",
            "day" => "1д",
            "день" => "1д",

            "7d" => "7д",
            "7д" => "7д",
            "week" => "7д",
            "неделя" => "7д",

            "1m" => "1мес",
            "1mo" => "1мес",
            "1м" => "1мес",
            "1мес" => "1мес",
            "month" => "1мес",
            "месяц" => "1мес",

            _ => normalized
        };
    }

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class DesignNavigationService : IAppNavigationService
    {
        public event Action<AppRoute>? RouteChanged;

        public AppRoute Current { get; private set; } = new(AppRoutes.Dashboard, "Дешборд", "Дешборд");

        public IReadOnlyList<AppRoute> Routes => [Current];

        public void Register(AppRoute route) { }

        public void Navigate(
            string routeKey,
            IReadOnlyDictionary<string, string>? parameters = null,
            string? titleOverride = null,
            string? breadcrumbOverride = null)
        {
            Current = new AppRoute(routeKey, titleOverride ?? routeKey, breadcrumbOverride ?? routeKey) { Parameters = parameters };
            RouteChanged?.Invoke(Current);
        }
    }
}

public interface IDashboardDataProvider
{
    DashboardSnapshot GetSnapshot();
}

public sealed record DashboardSnapshot(
    string Currency,
    IReadOnlyList<DashboardAssetSnapshot> Assets,
    IReadOnlyList<DashboardQuoteSnapshot> PreviousQuotes,
    IReadOnlyList<DashboardTransactionSnapshot> Transactions,
    IReadOnlyList<decimal> FallbackSeries)
{
    public static DashboardSnapshot Empty(string currency) => new(currency, [], [], [], []);
}

public sealed class DesignDashboardDataProvider : IDashboardDataProvider
{
    public DashboardSnapshot GetSnapshot()
    {
        Guid stockId = Guid.Parse("0a896663-ec39-4ebc-9b28-bf537f8f2fe0");
        Guid cryptoId = Guid.Parse("f3b4b129-5478-4965-923f-03ef74ca7c07");
        Guid cashId = Guid.Parse("59440f63-edfd-4655-8cb2-7f5f788a3df4");

        DashboardAssetSnapshot[] assets =
        [
            new(stockId, "Apple Inc.", "AAPL", 8m, 227.8m, 1822.4m, ["Акции"]),
            new(cryptoId, "Bitcoin", "BTC", 0.52m, 68000m, 35360m, ["Криптовалюта"]),
            new(cashId, "USD Cash", "USD", 1m, 12400m, 12400m, ["Наличность"])
        ];

        DashboardQuoteSnapshot[] previousQuotes =
        [
            new(stockId, 220m, DateTimeOffset.UtcNow.AddDays(-1)),
            new(cryptoId, 66500m, DateTimeOffset.UtcNow.AddDays(-1)),
            new(cashId, 12400m, DateTimeOffset.UtcNow.AddDays(-1))
        ];

        DashboardTransactionSnapshot[] transactions =
        [
            new(Guid.NewGuid(), "Apple Inc.", "AAPL", "Ордер покупки", new DateTimeOffset(2026, 3, 24, 12, 0, 0, TimeSpan.Zero), 227.8m, -12400m),
            new(Guid.NewGuid(), "Bitcoin", "BTC", "Дивиденд", new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero), 68000m, 420.15m),
            new(Guid.NewGuid(), "S&P 500 ETF", "SPY", "Ордер продажи", new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero), 520m, 55000m)
        ];

        decimal[] series = [42000m, 43500m, 42800m, 44700m, 46200m, 47100m, 48600m, 49800m, 49300m, 49582.4m];
        return new DashboardSnapshot("USD", assets, previousQuotes, transactions, series);
    }
}

public sealed class DashboardTimeframeViewModel : ViewModelBase
{
    private bool _isSelected;

    public DashboardTimeframeViewModel(string label, bool isSelected)
    {
        Label = label;
        _isSelected = isSelected;
    }

    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed record DashboardBarViewModel(
    string Label,
    double Height,
    bool IsActive,
    bool IsTooltipVisible,
    string TooltipText);

public sealed class DashboardAllocationRowViewModel
{
    public DashboardAllocationRowViewModel(string label, decimal value, decimal percent, string markerColor)
    {
        Label = label;
        Value = value;
        Percent = percent;
        MarkerBrush = new SolidColorBrush(Color.Parse(markerColor));
    }

    public string Label { get; }

    public decimal Value { get; }

    public decimal Percent { get; }

    public string PercentText => $"{Percent:0}%";

    public IBrush MarkerBrush { get; }
}

public sealed class DashboardTransactionRowViewModel
{
    public DashboardTransactionRowViewModel(
        string assetName,
        string dateText,
        string typeText,
        string amountText,
        string iconKind,
        bool isPositiveAmount)
    {
        AssetName = assetName;
        DateText = dateText;
        TypeText = typeText;
        AmountText = amountText;
        IconKind = iconKind;
        IsPositiveAmount = isPositiveAmount;
    }

    public string AssetName { get; }

    public string DateText { get; }

    public string TypeText { get; }

    public string AmountText { get; }

    public string IconKind { get; }

    public bool IsPositiveAmount { get; }

    public bool IsBitcoin => IconKind.Equals("bitcoin", StringComparison.OrdinalIgnoreCase);

    public bool IsSp => IconKind.Equals("sp", StringComparison.OrdinalIgnoreCase);

    public bool IsBuy => TypeText.Contains("покуп", StringComparison.OrdinalIgnoreCase);

    public bool IsDividend => TypeText.Contains("дивид", StringComparison.OrdinalIgnoreCase)
        || TypeText.Contains("staking", StringComparison.OrdinalIgnoreCase)
        || TypeText.Contains("airdrop", StringComparison.OrdinalIgnoreCase);

    public bool IsSell => TypeText.Contains("продаж", StringComparison.OrdinalIgnoreCase)
        || TypeText.Contains("комисс", StringComparison.OrdinalIgnoreCase)
        || TypeText.Contains("налог", StringComparison.OrdinalIgnoreCase);

    public string IconText => IconKind.ToLowerInvariant() switch
    {
        "bitcoin" => "₿",
        "sp" => "S&P",
        _ => IconKind.Length <= 3 ? IconKind : IconKind[..3]
    };
}
