using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Proxima.Core.Application.Analytics.Dashboard;
using Proxima.Core.Application.Dashboard;
using Proxima.App.Common.Commands;
using Proxima.App.Navigation;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.Core.Application.Transactions;
using Proxima.Core.Domain.Transactions;

namespace Proxima.App.Views.Dashboard;

public sealed class DashboardViewModel : ViewModelBase
{
    private static readonly string[] AllocationPalette = ["#003658", "#1D7329", "#B7D5E4", "#A67C00", "#7158E2", "#7D8793"];

    private readonly IDashboardService _dashboardService;
    private readonly IShellState _shellState;
    private readonly IAppNavigationService _navigation;
    private readonly ITransactionService _transactionService;
    private readonly IRuntimeDataInvalidation _dataInvalidation;
    private readonly RelayCommand _selectTimeframeCommand;
    private readonly RelayCommand _moreTransactionsCommand;
    private IReadOnlyList<DashboardTransactionRowViewModel> _allTransactions = [];
    private DashboardOverview _overview = DashboardOverview.Empty("USD");
    private bool _isShowingAllTransactions;
    private string _searchQuery = string.Empty;
    private string _selectedTimeframe = "1д";

    public DashboardViewModel(
        IDashboardService dashboardService,
        IShellState shellState,
        IRuntimeDataInvalidation dataInvalidation,
        IAppNavigationService navigation,
        ITransactionService transactionService)
    {
        _dashboardService = dashboardService;
        _shellState = shellState;
        _navigation = navigation;
        _transactionService = transactionService;
        _dataInvalidation = dataInvalidation;

        _selectTimeframeCommand = new RelayCommand(SelectTimeframe);
        _moreTransactionsCommand = new RelayCommand(_ => ToggleTransactionsLimit());

        Timeframes =
        [
            new DashboardTimeframeViewModel("1д", true),
            new DashboardTimeframeViewModel("7д", false),
            new DashboardTimeframeViewModel("1мес", false)
        ];

        Bars = [];
        AllocationRows = [];
        VisibleTransactions = [];

        _shellState.PortfolioChanged += (_, _) => _ = LoadAsync();
        dataInvalidation.DataInvalidated += (_, _) => _ = LoadAsync();

        _ = LoadAsync();
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

                BuildBars(_overview);
                MonthlyGrowthText = FormatMoney(CalculatePeriodGrowth(SliceSeries(_overview.FallbackSeries, SelectedTimeframe)), _overview.Currency, showPlus: true);
                OnPropertyChanged(nameof(MonthlyGrowthText));
            }
        }
    }

    public ICommand SelectTimeframeCommand => _selectTimeframeCommand;

    public ICommand MoreTransactionsCommand => _moreTransactionsCommand;

    public string MoreTransactionsButtonText => _isShowingAllTransactions ? "Скрыть транзакции  ↑" : "Все транзакции  ↓";

    private async Task LoadAsync()
    {
        try
        {
            _overview = await _dashboardService
                .GetOverviewAsync(_shellState.CurrentPortfolioId)
                .ConfigureAwait(true);
        }
        catch (Exception)
        {
            _overview = DashboardOverview.Empty("USD");
        }

        decimal total = PortfolioDashboardCalculator.CalculateTotalValue(_overview.Assets);
        TotalPortfolioValueText = FormatMoney(total, _overview.Currency);

        Delta24h delta = PortfolioDashboardCalculator.Calculate24hDelta(_overview.Assets, _overview.PreviousQuotes);
        GrowthPillText = FormatDelta(delta);
        MonthlyGrowthText = FormatMoney(CalculatePeriodGrowth(SliceSeries(_overview.FallbackSeries, SelectedTimeframe)), _overview.Currency, showPlus: true);

        BuildBars(_overview);
        BuildAllocation(_overview);
        BuildTransactions(_overview);
        ApplyTransactionFilter();
        BuildRiskAndBalanceSummary(_overview);

        OnPropertyChanged(nameof(TotalPortfolioValueText));
        OnPropertyChanged(nameof(GrowthPillText));
        OnPropertyChanged(nameof(MonthlyGrowthText));
        OnPropertyChanged(nameof(RiskLevelText));
        OnPropertyChanged(nameof(BalanceBlockTitle));
        OnPropertyChanged(nameof(BalanceBlockSubtitle));
        OnPropertyChanged(nameof(AllocationValues));
    }

    private void BuildBars(DashboardOverview snapshot)
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
            double height = 72 + normalized * 134;
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
            "1д" => Math.Min(2, series.Count),
            "7д" => Math.Min(8, series.Count),
            _ => Math.Min(30, series.Count)
        };

        return series.Skip(Math.Max(0, series.Count - take)).ToArray();
    }

    private void BuildAllocation(DashboardOverview snapshot)
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

    private void BuildTransactions(DashboardOverview snapshot)
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

        if (!_isShowingAllTransactions)
        {
            query = query.Take(3);
        }

        VisibleTransactions.Clear();

        foreach (DashboardTransactionRowViewModel item in query)
        {
            VisibleTransactions.Add(item);
        }
    }

    private void BuildRiskAndBalanceSummary(DashboardOverview snapshot)
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

    private DashboardTransactionRowViewModel MapTransaction(DashboardTransactionRow item)
    {
        bool positive = item.GrossAmount >= 0m;
        string iconKind = ResolveIconKind(item.AssetName, item.Ticker);
        return new DashboardTransactionRowViewModel(
            item.TransactionId,
            item.AssetName,
            FormatDate(item.TradeDate),
            item.TypeLabel,
            FormatMoney(item.GrossAmount, _overview.Currency, showPlus: true),
            iconKind,
            positive,
            () => EditTransaction(item),
            () => _ = DeleteTransactionAsync(item.TransactionId));
    }

    private void ToggleTransactionsLimit()
    {
        _isShowingAllTransactions = !_isShowingAllTransactions;
        OnPropertyChanged(nameof(MoreTransactionsButtonText));
        ApplyTransactionFilter();
    }

    private void EditTransaction(DashboardTransactionRow transaction)
    {
        SearchQuery = transaction.AssetName;
    }

    private async Task DeleteTransactionAsync(Guid transactionId)
    {
        TransactionOperationResult result = await _transactionService
            .ArchiveAsync(_shellState.CurrentPortfolioId, transactionId, CancellationToken.None)
            .ConfigureAwait(true);

        if (!result.Succeeded)
        {
            return;
        }

        _dataInvalidation.Invalidate("dashboard-transaction-deleted");
        _ = LoadAsync();
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

}
