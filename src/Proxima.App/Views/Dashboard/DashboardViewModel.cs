using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.Analytics.Dashboard;
using Proxima.App.ViewModels;

namespace Proxima.App.Views.Dashboard;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardDataProvider _dataProvider;
    private readonly DelegateCommand _sortByAssetCommand;
    private readonly DelegateCommand _sortByTypeCommand;
    private readonly DelegateCommand _sortByDateCommand;
    private readonly DelegateCommand _sortByAmountCommand;
    private readonly DelegateCommand _sortByPriceCommand;

    private IReadOnlyList<TransactionRowItemViewModel> _allTransactions = [];
    private string _searchQuery = string.Empty;
    private string _selectedTimeframe = "7 дней";
    private string _sortKey = "date";
    private bool _sortDescending = true;
    private bool _isTransactionsLoading;

    public DashboardViewModel(IDashboardDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
        Timeframes = new ObservableCollection<string> { "1 день", "7 дней", "месяц" };
        PortfolioValueSeries = [];
        AllocationRows = [];
        VisibleTransactions = [];

        _sortByAssetCommand = new DelegateCommand(_ => SortBy("asset"));
        _sortByTypeCommand = new DelegateCommand(_ => SortBy("type"));
        _sortByDateCommand = new DelegateCommand(_ => SortBy("date"));
        _sortByAmountCommand = new DelegateCommand(_ => SortBy("amount"));
        _sortByPriceCommand = new DelegateCommand(_ => SortBy("price"));

        Load();
    }

    public ObservableCollection<string> Timeframes { get; }

    public ObservableCollection<decimal> PortfolioValueSeries { get; }

    public ObservableCollection<AllocationRowViewModel> AllocationRows { get; }

    public ObservableCollection<TransactionRowItemViewModel> VisibleTransactions { get; }

    public ICommand SortByAssetCommand => _sortByAssetCommand;

    public ICommand SortByTypeCommand => _sortByTypeCommand;

    public ICommand SortByDateCommand => _sortByDateCommand;

    public ICommand SortByAmountCommand => _sortByAmountCommand;

    public ICommand SortByPriceCommand => _sortByPriceCommand;

    public string TotalPortfolioValueText { get; private set; } = "$0.00";

    public string Delta24hText { get; private set; } = "0.00";

    public string Delta24hPercentText { get; private set; } = "0.00%";

    public string Delta24hKind { get; private set; } = "Neutral";

    public string AllocationCountText => AllocationRows.Count.ToString();

    public bool IsTransactionsLoading
    {
        get => _isTransactionsLoading;
        private set
        {
            if (SetProperty(ref _isTransactionsLoading, value))
            {
                OnPropertyChanged(nameof(HasTransactions));
                OnPropertyChanged(nameof(IsTransactionsEmpty));
            }
        }
    }

    public bool HasTransactions => !IsTransactionsLoading && VisibleTransactions.Count > 0;

    public bool IsTransactionsEmpty => !IsTransactionsLoading && VisibleTransactions.Count == 0;

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

    public string SelectedTimeframe
    {
        get => _selectedTimeframe;
        set
        {
            if (SetProperty(ref _selectedTimeframe, value))
            {
                LoadSeries();
            }
        }
    }

    public static DashboardViewModel CreateDesignData()
    {
        return new DashboardViewModel(new MockDashboardDataProvider());
    }

    private void Load()
    {
        DashboardSnapshot snapshot = _dataProvider.GetSnapshot();

        decimal total = PortfolioDashboardCalculator.CalculateTotalValue(snapshot.Assets);
        TotalPortfolioValueText = $"{total:N2} {snapshot.Currency}";

        Delta24h delta = PortfolioDashboardCalculator.Calculate24hDelta(snapshot.Assets, snapshot.PreviousQuotes);
        if (!delta.HasEnoughData)
        {
            Delta24hText = "Недостаточно данных";
            Delta24hPercentText = "0.00%";
            Delta24hKind = "Neutral";
        }
        else
        {
            Delta24hText = $"{delta.Absolute:+0.##;-0.##;0.##} {snapshot.Currency}";
            Delta24hPercentText = delta.Percent is null ? "0.00%" : $"{delta.Percent:+0.##;-0.##;0.##}%";
            Delta24hKind = delta.Absolute > 0m ? "Positive" : delta.Absolute < 0m ? "Negative" : "Neutral";
        }

        AllocationRows.Clear();
        IReadOnlyList<AllocationSlice> slices = PortfolioDashboardCalculator.BuildAllocationByTag(snapshot.Assets);
        decimal allocationTotal = slices.Sum(static slice => slice.Value);
        foreach (AllocationSlice slice in slices.Take(5))
        {
            decimal pct = allocationTotal == 0m ? 0m : slice.Value / allocationTotal * 100m;
            AllocationRows.Add(new AllocationRowViewModel(slice.Tag, pct));
        }

        _allTransactions = snapshot.Transactions
            .Select(item => new TransactionRowItemViewModel(
                $"{item.AssetName} ({item.Ticker})",
                item.TypeLabel,
                item.TradeDate,
                item.GrossAmount,
                item.Price,
                snapshot.Currency))
            .ToArray();

        LoadSeries();
        ApplyTransactionFilters();
        OnPropertyChanged(nameof(TotalPortfolioValueText));
        OnPropertyChanged(nameof(Delta24hText));
        OnPropertyChanged(nameof(Delta24hPercentText));
        OnPropertyChanged(nameof(Delta24hKind));
        OnPropertyChanged(nameof(AllocationCountText));
    }

    private void LoadSeries()
    {
        PortfolioValueSeries.Clear();

        DashboardSnapshot snapshot = _dataProvider.GetSnapshot();
        string analyticsWindow = SelectedTimeframe switch
        {
            "1 день" => "1D",
            "7 дней" => "7D",
            _ => "1M"
        };

        IReadOnlyList<(DateTimeOffset Time, decimal Value)> points = PortfolioDashboardCalculator.BuildHistorySeries(snapshot.Transactions, analyticsWindow);
        foreach ((DateTimeOffset _, decimal value) in points)
        {
            PortfolioValueSeries.Add(value);
        }

        if (PortfolioValueSeries.Count < 2)
        {
            PortfolioValueSeries.Clear();
            foreach (decimal fallback in snapshot.FallbackSeries)
            {
                PortfolioValueSeries.Add(fallback);
            }
        }
    }

    private void ApplyTransactionFilters()
    {
        IsTransactionsLoading = true;

        IEnumerable<TransactionRowItemViewModel> query = _allTransactions;
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string term = SearchQuery.Trim();
            query = query.Where(item =>
                item.AssetLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.TypeLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = _sortKey switch
        {
            "asset" => _sortDescending ? query.OrderByDescending(item => item.AssetLabel, StringComparer.OrdinalIgnoreCase) : query.OrderBy(item => item.AssetLabel, StringComparer.OrdinalIgnoreCase),
            "type" => _sortDescending ? query.OrderByDescending(item => item.TypeLabel, StringComparer.OrdinalIgnoreCase) : query.OrderBy(item => item.TypeLabel, StringComparer.OrdinalIgnoreCase),
            "amount" => _sortDescending ? query.OrderByDescending(item => item.Amount) : query.OrderBy(item => item.Amount),
            "price" => _sortDescending ? query.OrderByDescending(item => item.Price) : query.OrderBy(item => item.Price),
            _ => _sortDescending ? query.OrderByDescending(item => item.Date) : query.OrderBy(item => item.Date)
        };

        VisibleTransactions.Clear();
        foreach (TransactionRowItemViewModel item in query.Take(10))
        {
            VisibleTransactions.Add(item);
        }

        IsTransactionsLoading = false;
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(IsTransactionsEmpty));
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

    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        private readonly Action<object?> _execute = execute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record AllocationRowViewModel(string Label, decimal Percent)
{
    public string PercentText => $"{Percent:0.##}%";
}

public sealed record TransactionRowItemViewModel(string AssetLabel, string TypeLabel, DateTimeOffset Date, decimal Amount, decimal Price, string Currency)
{
    public string DateText => Date.LocalDateTime.ToString("dd.MM.yyyy");

    public string AmountText => $"{Amount:N2} {Currency}";

    public string PriceText => $"{Price:N2} {Currency}";
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
    IReadOnlyList<decimal> FallbackSeries);

public sealed class MockDashboardDataProvider : IDashboardDataProvider
{
    private readonly DashboardSnapshot _snapshot = BuildSnapshot();

    public DashboardSnapshot GetSnapshot() => _snapshot;

    private static DashboardSnapshot BuildSnapshot()
    {
        Guid aaplId = Guid.Parse("0a896663-ec39-4ebc-9b28-bf537f8f2fe0");
        Guid msftId = Guid.Parse("f3b4b129-5478-4965-923f-03ef74ca7c07");
        Guid bondId = Guid.Parse("59440f63-edfd-4655-8cb2-7f5f788a3df4");

        DashboardAssetSnapshot[] assets =
        [
            new(aaplId, "Apple Inc.", "AAPL", 12m, 185.30m, 2223.60m, ["Акции", "USD"]),
            new(msftId, "Microsoft", "MSFT", 9m, 421.25m, 3791.25m, ["Акции", "Big Tech"]),
            new(bondId, "Belarus Gov Bond", "BYGOV", 20m, 102.60m, 2052.00m, ["Облигации"])
        ];

        DashboardQuoteSnapshot[] previousQuotes =
        [
            new(aaplId, 182.10m, DateTimeOffset.UtcNow.AddDays(-1)),
            new(msftId, 417.40m, DateTimeOffset.UtcNow.AddDays(-1)),
            new(bondId, 102.10m, DateTimeOffset.UtcNow.AddDays(-1))
        ];

        DashboardTransactionSnapshot[] transactions =
        [
            new(Guid.NewGuid(), "Apple Inc.", "AAPL", "Buy", DateTimeOffset.UtcNow.AddHours(-5), 185.30m, 741.20m),
            new(Guid.NewGuid(), "Microsoft", "MSFT", "Buy", DateTimeOffset.UtcNow.AddDays(-1), 420.20m, 840.40m),
            new(Guid.NewGuid(), "Belarus Gov Bond", "BYGOV", "Buy", DateTimeOffset.UtcNow.AddDays(-2), 102.50m, 512.50m),
            new(Guid.NewGuid(), "Apple Inc.", "AAPL", "Sell", DateTimeOffset.UtcNow.AddDays(-4), 183.00m, 366.00m),
            new(Guid.NewGuid(), "Microsoft", "MSFT", "Dividend", DateTimeOffset.UtcNow.AddDays(-7), 0m, 42.00m)
        ];

        decimal[] series = [6200m, 6280m, 6340m, 6415m, 6480m, 6525m, 6610m];

        return new DashboardSnapshot("USD", assets, previousQuotes, transactions, series);
    }
}
