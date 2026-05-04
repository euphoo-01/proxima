using System.Collections.ObjectModel;
using System.Windows.Input;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.Application.Taxes;
using Proxima.Application.Transactions;
using Proxima.Reporting.Reports;

namespace Proxima.App.Views.Taxes;

public sealed class TaxesViewModel : ViewModelBase
{
    private readonly ITaxesReadModelProvider _provider;
    private readonly IShellState _shellState;
    private readonly DelegateCommand _recalculateCommand;
    private readonly DelegateCommand _exportPdfCommand;

    private int _selectedYear = DateTime.UtcNow.Year;
    private LegalProfileType _selectedProfile = LegalProfileType.Other;
    private bool _isLoading;
    private bool _hasError;
    private bool _isEmpty;
    private bool _isOfflineRate;
    private string _statusMessage = "Выберите параметры и выполните расчёт.";
    private string _offlineRateMessage = string.Empty;

    private string _taxableIncome = "—";
    private string _estimatedTax = "—";
    private string _taxSaved = "—";
    private string _currency = "USD";
    private string _summaryStatus = "Draft";

    public TaxesViewModel(ITaxesReadModelProvider provider, IShellState shellState, IRuntimeDataInvalidation dataInvalidation)
    {
        _provider = provider;
        _shellState = shellState;

        Years =
        [
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Year - 1,
            DateTime.UtcNow.Year - 2,
            DateTime.UtcNow.Year - 3
        ];

        Profiles = Enum.GetValues<LegalProfileType>();
        BreakdownRows = [];

        _recalculateCommand = new DelegateCommand(_ => _ = RecalculateAsync());
        _exportPdfCommand = new DelegateCommand(_ => _ = ExportPdfAsync());
        _shellState.PortfolioChanged += (_, _) => _ = RecalculateAsync();
        dataInvalidation.DataInvalidated += (_, _) => _ = RecalculateAsync();

        _ = RecalculateAsync();
    }

    public string Title => "Налоги";

    public string Subtitle => "Черновой расчёт для декларации РБ. Проверьте значения в официальных источниках.";

    public IReadOnlyList<int> Years { get; }

    public IReadOnlyList<LegalProfileType> Profiles { get; }

    public ObservableCollection<TaxBreakdownRowViewModel> BreakdownRows { get; }

    public ICommand RecalculateCommand => _recalculateCommand;

    public ICommand ExportPdfCommand => _exportPdfCommand;

    public int SelectedYear
    {
        get => _selectedYear;
        set => SetProperty(ref _selectedYear, value);
    }

    public LegalProfileType SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(HasContent));
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
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set
        {
            if (SetProperty(ref _isEmpty, value))
            {
                OnPropertyChanged(nameof(HasContent));
            }
        }
    }

    public bool HasContent => !IsLoading && !HasError && !IsEmpty;

    public bool IsOfflineRate
    {
        get => _isOfflineRate;
        private set => SetProperty(ref _isOfflineRate, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string OfflineRateMessage
    {
        get => _offlineRateMessage;
        private set => SetProperty(ref _offlineRateMessage, value);
    }

    public string TaxableIncome
    {
        get => _taxableIncome;
        private set => SetProperty(ref _taxableIncome, value);
    }

    public string EstimatedTax
    {
        get => _estimatedTax;
        private set => SetProperty(ref _estimatedTax, value);
    }

    public string TaxSaved
    {
        get => _taxSaved;
        private set => SetProperty(ref _taxSaved, value);
    }

    public string Currency
    {
        get => _currency;
        private set => SetProperty(ref _currency, value);
    }

    public string SummaryStatus
    {
        get => _summaryStatus;
        private set => SetProperty(ref _summaryStatus, value);
    }

    public string TaxDisclaimer => "Расчет носит информационный характер. Перед подачей декларации проверьте актуальные правила и официальные источники.";

    public static TaxesViewModel CreateDesignData()
    {
        return new TaxesViewModel(new DesignTaxesReadModelProvider(), new MockShellState(), new RuntimeDataInvalidation());
    }

    private async Task RecalculateAsync()
    {
        IsLoading = true;
        HasError = false;
        IsEmpty = false;
        IsOfflineRate = false;
        OfflineRateMessage = string.Empty;

        try
        {
            TaxScreenReadModel model = await _provider.GetAsync(SelectedYear, SelectedProfile, CancellationToken.None).ConfigureAwait(true);

            BreakdownRows.Clear();
            foreach (TaxBreakdownRow row in model.Breakdown)
            {
                BreakdownRows.Add(new TaxBreakdownRowViewModel(row.Name, row.Value, row.Note));
            }

            Currency = model.Currency;
            TaxableIncome = $"{model.TaxableBase:0.##} {model.Currency}";
            EstimatedTax = $"{model.TotalTaxDue:0.##} {model.Currency}";
            TaxSaved = $"{model.TaxSaved:0.##} {model.Currency}";
            SummaryStatus = model.Status;
            StatusMessage = model.Message;
            IsEmpty = model.IsEmpty;
            IsOfflineRate = model.IsOfflineRate;
            OfflineRateMessage = model.OfflineRateMessage;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Не удалось выполнить расчет: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportPdfAsync()
    {
        TaxExportResult result = await _provider.ExportPdfAsync(SelectedYear, SelectedProfile, CancellationToken.None).ConfigureAwait(true);
        StatusMessage = result.Message;
    }

    public interface ITaxesReadModelProvider
    {
        Task<TaxScreenReadModel> GetAsync(int year, LegalProfileType profile, CancellationToken cancellationToken);

        Task<TaxExportResult> ExportPdfAsync(int year, LegalProfileType profile, CancellationToken cancellationToken);
    }

    public sealed class AppTaxesReadModelProvider : ITaxesReadModelProvider
    {
        private readonly ITransactionService _transactions;
        private readonly ITaxCalculator _taxCalculator;
        private readonly IReportService _reportService;
        private readonly IShellState _shellState;

        public AppTaxesReadModelProvider(
            ITransactionService transactions,
            ITaxCalculator taxCalculator,
            IReportService reportService,
            IShellState shellState)
        {
            _transactions = transactions;
            _taxCalculator = taxCalculator;
            _reportService = reportService;
            _shellState = shellState;
        }

        public async Task<TaxScreenReadModel> GetAsync(int year, LegalProfileType profile, CancellationToken cancellationToken)
        {
            IReadOnlyList<Proxima.Domain.Transactions.PortfolioTransaction> transactions = await _transactions
                .ListActiveAsync(_shellState.CurrentPortfolioId, cancellationToken)
                .ConfigureAwait(false);

            if (transactions.Count == 0)
            {
                return TaxScreenReadModel.Empty("Нет транзакций для расчета налогов.");
            }

            List<TaxTransactionSnapshot> snapshots = transactions
                .Select(item => new TaxTransactionSnapshot(item.TradeDate, item.Type, item.GrossAmount, item.FeeAmount, item.TaxAmount, item.Currency))
                .ToList();

            TaxCalculationResult calculation = await _taxCalculator
                .CalculateAsync(snapshots, year, profile, "BYN", cancellationToken)
                .ConfigureAwait(false);

            bool offlineRate = !calculation.Succeeded || string.Equals(calculation.RateSource, "mock", StringComparison.OrdinalIgnoreCase);
            string offlineRateMessage = offlineRate
                ? "Курс валют недоступен или используется fallback-источник. Проверьте интернет и источник NBRB."
                : string.Empty;

            IReadOnlyList<TaxBreakdownRow> rows =
            [
                new TaxBreakdownRow("Дивиденды", calculation.Dividends, "Учет дивидендного дохода"),
                new TaxBreakdownRow("Влияние курса", calculation.CurrencyEffect, "Конвертация в базовую валюту"),
                new TaxBreakdownRow("Вычеты", calculation.TaxSaved, "Льготы и вычеты"),
                new TaxBreakdownRow("Перенос убытков", calculation.Losses, "Убытки прошлых периодов")
            ];

            return new TaxScreenReadModel(
                IsEmpty: false,
                Message: calculation.Message,
                Status: calculation.Succeeded ? "Draft" : "Требуется проверка",
                Currency: "BYN",
                TaxableBase: calculation.TaxableBase,
                TotalTaxDue: calculation.TaxDue,
                TaxSaved: calculation.TaxSaved,
                IsOfflineRate: offlineRate,
                OfflineRateMessage: offlineRateMessage,
                Breakdown: rows,
                LegalDisclaimer: "Расчет носит информационный характер. Перед подачей декларации проверьте актуальные правила и официальные источники.");
        }

        public async Task<TaxExportResult> ExportPdfAsync(int year, LegalProfileType profile, CancellationToken cancellationToken)
        {
            TaxScreenReadModel model = await GetAsync(year, profile, cancellationToken).ConfigureAwait(false);

            TaxReportRequest request = new(
                UserDisplayName: _shellState.CurrentPortfolioName,
                TaxProfile: profile.ToString(),
                Year: year,
                TaxableBase: model.TaxableBase,
                TotalTaxDue: model.TotalTaxDue,
                ExchangeRateNotes: model.IsOfflineRate ? "Fallback rate source" : "NBRB/provider",
                Dividends: model.Breakdown.FirstOrDefault(x => x.Name == "Дивиденды")?.Value ?? 0m,
                TransactionCount: model.Breakdown.Count,
                CalculationVersion: "tax-draft-v1",
                LegalDisclaimer: model.LegalDisclaimer,
                OutputDirectory: ProximaReportingComposition.GetDefaultReportDirectory());

            ReportExportResult export = await _reportService.ExportTaxPdfAsync(request, cancellationToken).ConfigureAwait(false);
            return new TaxExportResult(export.Succeeded, export.Message);
        }
    }

    private sealed class DesignTaxesReadModelProvider : ITaxesReadModelProvider
    {
        public Task<TaxScreenReadModel> GetAsync(int year, LegalProfileType profile, CancellationToken cancellationToken)
        {
            IReadOnlyList<TaxBreakdownRow> rows =
            [
                new TaxBreakdownRow("Дивиденды", 412.2m, "Доход по дивидендам"),
                new TaxBreakdownRow("Влияние курса", 219.4m, "Оценка по курсу BYN"),
                new TaxBreakdownRow("Вычеты", 128.8m, "Льготы и вычеты"),
                new TaxBreakdownRow("Перенос убытков", 94.3m, "Применено к базе")
            ];

            TaxScreenReadModel model = new(
                IsEmpty: false,
                Message: "Черновик расчета обновлен.",
                Status: "Draft",
                Currency: "BYN",
                TaxableBase: 12547.82m,
                TotalTaxDue: 1631.22m,
                TaxSaved: 128.8m,
                IsOfflineRate: true,
                OfflineRateMessage: "Нет ответа от провайдера курса. Показан fallback-режим.",
                Breakdown: rows,
                LegalDisclaimer: "Расчет носит информационный характер. Перед подачей декларации проверьте актуальные правила и официальные источники.");

            return Task.FromResult(model);
        }

        public Task<TaxExportResult> ExportPdfAsync(int year, LegalProfileType profile, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TaxExportResult(true, "PDF-черновик сформирован (design data)."));
        }
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

public sealed record TaxScreenReadModel(
    bool IsEmpty,
    string Message,
    string Status,
    string Currency,
    decimal TaxableBase,
    decimal TotalTaxDue,
    decimal TaxSaved,
    bool IsOfflineRate,
    string OfflineRateMessage,
    IReadOnlyList<TaxBreakdownRow> Breakdown,
    string LegalDisclaimer)
{
    public static TaxScreenReadModel Empty(string message)
    {
        return new TaxScreenReadModel(
            true,
            message,
            "Пусто",
            "BYN",
            0m,
            0m,
            0m,
            false,
            string.Empty,
            [],
            "Расчет носит информационный характер. Перед подачей декларации проверьте актуальные правила и официальные источники.");
    }
}

public sealed record TaxBreakdownRow(string Name, decimal Value, string Note);

public sealed record TaxBreakdownRowViewModel(string Name, decimal Value, string Note)
{
    public string ValueText => $"{Value:0.##}";
}

public sealed record TaxExportResult(bool Succeeded, string Message);
