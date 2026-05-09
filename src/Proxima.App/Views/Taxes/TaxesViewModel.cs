using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.Application.Taxes;
using Proxima.Application.Transactions;
using Proxima.Reporting.Reports;

namespace Proxima.App.Views.Taxes;

public sealed class TaxesViewModel : ViewModelBase
{
    private const string BaseCurrency = "BYN";
    private const decimal IncomeThreshold = 200_000m;

    private readonly ITaxesReadModelProvider _provider;
    private readonly IShellState _shellState;
    private readonly DelegateCommand _recalculateCommand;
    private readonly DelegateCommand _exportPdfCommand;

    private int _selectedYear = DateTime.UtcNow.Year;
    private TaxProfileOption? _selectedProfile;
    private bool _isLoading;
    private bool _hasError;
    private bool _isEmpty;
    private bool _isOfflineRate;
    private string _statusMessage = "Выберите период и выполните расчет.";
    private string _offlineRateMessage = string.Empty;

    private decimal _taxableBase;
    private decimal _totalTaxDue;
    private decimal _taxSaved;
    private decimal _realizedGains;
    private decimal _dividends;
    private decimal _currencyEffect;
    private decimal _fees;
    private decimal _losses;
    private decimal _baseRatePercent = 13m;
    private decimal _dividendRatePercent = 13m;
    private decimal _exemptionAmount = 2_000m;
    private int _transactionCount;
    private string _currency = BaseCurrency;
    private string _summaryStatus = "Ожидается";
    private string _rateSourceText = "Источник курса не выбран";
    private string _calculationVersion = "tax-draft-v1";

    public TaxesViewModel(ITaxesReadModelProvider provider, IShellState shellState, IRuntimeDataInvalidation dataInvalidation)
    {
        _provider = provider;
        _shellState = shellState;

        Years = BuildYears(DateTime.UtcNow.Year);
        Profiles = BuildProfiles();
        _selectedProfile = Profiles.FirstOrDefault(static item => item.Profile == LegalProfileType.PhysicalPerson) ?? Profiles.FirstOrDefault();

        BreakdownRows = [];
        DeadlineRows = [];

        _recalculateCommand = new DelegateCommand(_ => _ = RecalculateAsync(), _ => !IsLoading);
        _exportPdfCommand = new DelegateCommand(_ => _ = ExportPdfAsync(), _ => !IsLoading && HasContent);
        _shellState.PortfolioChanged += (_, _) => _ = RecalculateAsync();
        dataInvalidation.DataInvalidated += (_, _) => _ = RecalculateAsync();

        RebuildDeadlines();
        _ = RecalculateAsync();
    }

    public string Title => "Налоги";

    public string Subtitle => "Черновой расчет налогов для РБ с учетом курсовых разниц, дивидендов и льгот.";

    public IReadOnlyList<int> Years { get; }

    public IReadOnlyList<TaxProfileOption> Profiles { get; }

    public ObservableCollection<TaxBreakdownRowViewModel> BreakdownRows { get; }

    public ObservableCollection<TaxDeadlineRowViewModel> DeadlineRows { get; }

    public ICommand RecalculateCommand => _recalculateCommand;

    public ICommand ExportPdfCommand => _exportPdfCommand;

    public int SelectedYear
    {
        get => _selectedYear;
        set
        {
            if (SetProperty(ref _selectedYear, value))
            {
                RebuildDeadlines();
                OnPropertyChanged(nameof(DeadlineTitle));
            }
        }
    }

    public TaxProfileOption? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                OnPropertyChanged(nameof(SelectedProfileName));
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
                OnContentStateChanged();
                _recalculateCommand.RaiseCanExecuteChanged();
                _exportPdfCommand.RaiseCanExecuteChanged();
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
                OnContentStateChanged();
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
                OnContentStateChanged();
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

    public decimal TaxableBase
    {
        get => _taxableBase;
        private set
        {
            if (SetProperty(ref _taxableBase, value))
            {
                OnPropertyChanged(nameof(TaxableBaseText));
                OnPropertyChanged(nameof(TaxableBaseShortText));
                OnPropertyChanged(nameof(IncomeThresholdProgress));
                OnPropertyChanged(nameof(IncomeThresholdProgressText));
                OnPropertyChanged(nameof(IncomeThresholdCaption));
            }
        }
    }

    public decimal TotalTaxDue
    {
        get => _totalTaxDue;
        private set
        {
            if (SetProperty(ref _totalTaxDue, value))
            {
                OnPropertyChanged(nameof(TotalTaxDueText));
            }
        }
    }

    public decimal TaxSaved
    {
        get => _taxSaved;
        private set
        {
            if (SetProperty(ref _taxSaved, value))
            {
                OnPropertyChanged(nameof(TaxSavedText));
            }
        }
    }

    public decimal RealizedGains
    {
        get => _realizedGains;
        private set => SetProperty(ref _realizedGains, value);
    }

    public decimal Dividends
    {
        get => _dividends;
        private set => SetProperty(ref _dividends, value);
    }

    public decimal CurrencyEffect
    {
        get => _currencyEffect;
        private set => SetProperty(ref _currencyEffect, value);
    }

    public decimal Fees
    {
        get => _fees;
        private set => SetProperty(ref _fees, value);
    }

    public decimal Losses
    {
        get => _losses;
        private set => SetProperty(ref _losses, value);
    }

    public decimal BaseRatePercent
    {
        get => _baseRatePercent;
        private set
        {
            if (SetProperty(ref _baseRatePercent, value))
            {
                OnPropertyChanged(nameof(LocalTaxRateText));
            }
        }
    }

    public decimal DividendRatePercent
    {
        get => _dividendRatePercent;
        private set
        {
            if (SetProperty(ref _dividendRatePercent, value))
            {
                OnPropertyChanged(nameof(DividendTaxRateText));
            }
        }
    }

    public decimal ExemptionAmount
    {
        get => _exemptionAmount;
        private set
        {
            if (SetProperty(ref _exemptionAmount, value))
            {
                OnPropertyChanged(nameof(ExemptionAmountText));
            }
        }
    }

    public int TransactionCount
    {
        get => _transactionCount;
        private set
        {
            if (SetProperty(ref _transactionCount, value))
            {
                OnPropertyChanged(nameof(TransactionCountText));
            }
        }
    }

    public string Currency
    {
        get => _currency;
        private set
        {
            if (SetProperty(ref _currency, value))
            {
                OnPropertyChanged(nameof(TaxableBaseText));
                OnPropertyChanged(nameof(TaxableBaseShortText));
                OnPropertyChanged(nameof(TotalTaxDueText));
                OnPropertyChanged(nameof(TaxSavedText));
                OnPropertyChanged(nameof(ExemptionAmountText));
                OnPropertyChanged(nameof(IncomeThresholdCaption));
            }
        }
    }

    public string SummaryStatus
    {
        get => _summaryStatus;
        private set => SetProperty(ref _summaryStatus, value);
    }

    public string RateSourceText
    {
        get => _rateSourceText;
        private set => SetProperty(ref _rateSourceText, value);
    }

    public string CalculationVersion
    {
        get => _calculationVersion;
        private set => SetProperty(ref _calculationVersion, value);
    }

    public string SelectedProfileName => SelectedProfile?.Title ?? "Налоговый профиль";

    public string TotalTaxDueText => FormatMoney(TotalTaxDue, Currency);

    public string TaxableBaseText => FormatMoney(TaxableBase, Currency);

    public string TaxableBaseShortText => FormatMoney(TaxableBase, Currency, compact: true);

    public string TaxSavedText => FormatMoney(TaxSaved, Currency);

    public string ExemptionAmountText => FormatMoney(ExemptionAmount, Currency);

    public string LocalTaxRateText => $"{BaseRatePercent:0.##}% РБ";

    public string DividendTaxRateText => $"{DividendRatePercent:0.##}% дивиденды";

    public string ForeignWithholdingRateText => "10% США (W-8BEN)";

    public string IncomeThresholdProgressText => $"{IncomeThresholdProgress:0}%";

    public double IncomeThresholdProgress => IncomeThreshold <= 0m ? 0d : Convert.ToDouble(Math.Clamp(TaxableBase / IncomeThreshold * 100m, 0m, 100m));

    public string IncomeThresholdCaption => $"{FormatMoney(TaxableBase, Currency, compact: true)} / {FormatMoney(IncomeThreshold, Currency, compact: true)}";

    public string DeadlineTitle => $"Дедлайны {SelectedYear}";

    public string TransactionCountText => TransactionCount == 1 ? "1 операция" : $"{TransactionCount} операций";

    public string TaxDisclaimer => "Расчет носит информационный характер и не является юридической консультацией. Перед подачей декларации проверьте актуальные правила и официальные источники.";

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
            LegalProfileType profile = SelectedProfile?.Profile ?? LegalProfileType.PhysicalPerson;
            TaxScreenReadModel model = await _provider.GetAsync(SelectedYear, profile, CancellationToken.None).ConfigureAwait(true);

            BreakdownRows.Clear();
            foreach (TaxBreakdownRow row in model.Breakdown)
            {
                BreakdownRows.Add(new TaxBreakdownRowViewModel(row.Name, FormatMoney(row.Value, model.Currency), row.Note, row.Kind));
            }

            Currency = model.Currency;
            TaxableBase = model.TaxableBase;
            TotalTaxDue = model.TotalTaxDue;
            TaxSaved = model.TaxSaved;
            RealizedGains = model.RealizedGains;
            Dividends = model.Dividends;
            CurrencyEffect = model.CurrencyEffect;
            Fees = model.Fees;
            Losses = model.Losses;
            BaseRatePercent = model.BaseRatePercent;
            DividendRatePercent = model.DividendRatePercent;
            ExemptionAmount = model.ExemptionAmount;
            TransactionCount = model.TransactionCount;
            SummaryStatus = model.Status;
            StatusMessage = model.Message;
            RateSourceText = model.RateSourceText;
            CalculationVersion = model.CalculationVersion;
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
        if (IsLoading || !HasContent)
        {
            return;
        }

        LegalProfileType profile = SelectedProfile?.Profile ?? LegalProfileType.PhysicalPerson;
        TaxExportResult result = await _provider.ExportPdfAsync(SelectedYear, profile, CancellationToken.None).ConfigureAwait(true);
        StatusMessage = result.Message;
    }

    private void RebuildDeadlines()
    {
        DeadlineRows.Clear();
        DeadlineRows.Add(new TaxDeadlineRowViewModel("МАРТ", "31", "Заполнение декларации", true));
        DeadlineRows.Add(new TaxDeadlineRowViewModel("ИЮНЬ", "01", "Оплата налогов", false));
        DeadlineRows.Add(new TaxDeadlineRowViewModel("АВГ", "01", "Дивиденды и купоны", false));
        DeadlineRows.Add(new TaxDeadlineRowViewModel("СЕНТ", "01", "Крипто и airdrop", false));
        DeadlineRows.Add(new TaxDeadlineRowViewModel("ОКТ", "01", "Сверка документов", false));
        DeadlineRows.Add(new TaxDeadlineRowViewModel("НОЯБ", "01", "Финальная проверка", false));
    }

    private void OnContentStateChanged()
    {
        OnPropertyChanged(nameof(HasContent));
        _exportPdfCommand.RaiseCanExecuteChanged();
    }

    private static IReadOnlyList<int> BuildYears(int currentYear)
    {
        return [currentYear, currentYear - 1, currentYear - 2, currentYear - 3];
    }

    private static IReadOnlyList<TaxProfileOption> BuildProfiles()
    {
        return
        [
            new TaxProfileOption(LegalProfileType.PhysicalPerson, "Физическое лицо", "Стандартный профиль резидента РБ"),
            new TaxProfileOption(LegalProfileType.SelfEmployed, "Самозанятый", "Для раздельной проверки доходов"),
            new TaxProfileOption(LegalProfileType.IndividualEntrepreneur, "ИП", "Предпринимательский профиль"),
            new TaxProfileOption(LegalProfileType.LLC, "ООО", "Корпоративная ставка с надбавкой"),
            new TaxProfileOption(LegalProfileType.JSC, "ЗАО/ОАО", "Корпоративная ставка с надбавкой")
        ];
    }

    private static string FormatMoney(decimal value, string currency, bool compact = false)
    {
        decimal abs = Math.Abs(value);
        string sign = value < 0m ? "-" : string.Empty;

        if (compact && abs >= 1_000_000m)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{sign}{abs / 1_000_000m:0.##} млн {currency}");
        }

        if (compact && abs >= 1_000m)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{sign}{abs / 1_000m:0.##} тыс. {currency}");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:N2} {currency}");
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
                return TaxScreenReadModel.Empty("Нет транзакций для расчета налогов. Добавьте сделки, дивиденды или комиссии в портфель.");
            }

            List<TaxTransactionSnapshot> snapshots = transactions
                .Select(item => new TaxTransactionSnapshot(item.TradeDate, item.Type, item.GrossAmount, item.FeeAmount, item.TaxAmount, item.Currency))
                .ToList();

            int yearTransactionCount = snapshots.Count(item => item.TradeDate.Year == year);
            if (yearTransactionCount == 0)
            {
                return TaxScreenReadModel.Empty($"За {year} год нет транзакций для расчета налогов.");
            }

            TaxCalculationResult calculation = await _taxCalculator
                .CalculateAsync(snapshots, year, profile, BaseCurrency, cancellationToken)
                .ConfigureAwait(false);

            bool offlineRate = !calculation.Succeeded || calculation.RateSource.Contains("mock", StringComparison.OrdinalIgnoreCase);
            string offlineRateMessage = offlineRate
                ? "Курс валют недоступен или используется fallback-источник. Проверьте интернет и настройки провайдера курсов."
                : string.Empty;

            IReadOnlyList<TaxBreakdownRow> rows = BuildBreakdownRows(calculation);

            string status = calculation.Succeeded ? "Ожидается" : "Требуется проверка";
            string rateText = BuildRateSourceText(calculation);

            return new TaxScreenReadModel(
                IsEmpty: false,
                Message: calculation.Message,
                Status: status,
                Currency: BaseCurrency,
                TaxableBase: calculation.TaxableBase,
                TotalTaxDue: calculation.TaxDue,
                TaxSaved: calculation.TaxSaved,
                RealizedGains: calculation.RealizedGains,
                Dividends: calculation.Dividends,
                Fees: calculation.Fees,
                CurrencyEffect: calculation.CurrencyEffect,
                Losses: calculation.Losses,
                BaseRatePercent: calculation.RuleSet.BaseRatePercent,
                DividendRatePercent: calculation.RuleSet.DividendRatePercent,
                ExemptionAmount: calculation.RuleSet.ExemptionAmount,
                TransactionCount: yearTransactionCount,
                RateSourceText: rateText,
                CalculationVersion: calculation.RuleSet.Version,
                IsOfflineRate: offlineRate,
                OfflineRateMessage: offlineRateMessage,
                Breakdown: rows,
                LegalDisclaimer: calculation.RuleSet.Disclaimer);
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
                ExchangeRateNotes: model.RateSourceText,
                Dividends: model.Dividends,
                TransactionCount: model.TransactionCount,
                CalculationVersion: model.CalculationVersion,
                LegalDisclaimer: model.LegalDisclaimer,
                OutputDirectory: ProximaReportingComposition.GetDefaultReportDirectory());

            ReportExportResult export = await _reportService.ExportTaxPdfAsync(request, cancellationToken).ConfigureAwait(false);
            return new TaxExportResult(export.Succeeded, export.Message);
        }

        private static IReadOnlyList<TaxBreakdownRow> BuildBreakdownRows(TaxCalculationResult calculation)
        {
            return
            [
                new TaxBreakdownRow("Реализованная прибыль", calculation.RealizedGains, "Продажи активов за выбранный год", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Дивиденды", calculation.Dividends, "Дивидендный доход и купоны", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Курсовая разница", calculation.CurrencyEffect, "Конвертация операций в BYN", TaxBreakdownKind.Currency),
                new TaxBreakdownRow("Комиссии и удержания", calculation.Fees, "Комиссии брокера и удержанные налоги", TaxBreakdownKind.Deduction),
                new TaxBreakdownRow("Льготы", calculation.TaxSaved, "Примененный необлагаемый лимит", TaxBreakdownKind.Benefit),
                new TaxBreakdownRow("Перенос убытков", calculation.Losses, "Убытки, уменьшающие налоговую базу", TaxBreakdownKind.Deduction)
            ];
        }

        private static string BuildRateSourceText(TaxCalculationResult calculation)
        {
            string source = string.IsNullOrWhiteSpace(calculation.RateSource)
                ? "источник не указан"
                : calculation.RateSource;

            return calculation.RateDate is null
                ? $"Курс: {source}"
                : $"Курс: {source}, дата {calculation.RateDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
        }
    }

    private sealed class DesignTaxesReadModelProvider : ITaxesReadModelProvider
    {
        public Task<TaxScreenReadModel> GetAsync(int year, LegalProfileType profile, CancellationToken cancellationToken)
        {
            IReadOnlyList<TaxBreakdownRow> rows =
            [
                new TaxBreakdownRow("Реализованная прибыль", 97_850m, "Продажи активов за выбранный год", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Дивиденды", 11_200m, "Дивидендный доход и купоны", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Курсовая разница", 800m, "Конвертация операций в BYN", TaxBreakdownKind.Currency),
                new TaxBreakdownRow("Комиссии и удержания", -1_420m, "Комиссии брокера и удержанные налоги", TaxBreakdownKind.Deduction),
                new TaxBreakdownRow("Льготы", 4_215m, "Примененный необлагаемый лимит", TaxBreakdownKind.Benefit),
                new TaxBreakdownRow("Перенос убытков", -640m, "Убытки, уменьшающие налоговую базу", TaxBreakdownKind.Deduction)
            ];

            TaxScreenReadModel model = new(
                IsEmpty: false,
                Message: "Черновик расчета обновлен.",
                Status: "Ожидается",
                Currency: BaseCurrency,
                TaxableBase: 109_850m,
                TotalTaxDue: 14_280.50m,
                TaxSaved: 4_215m,
                RealizedGains: 97_850m,
                Dividends: 11_200m,
                Fees: -1_420m,
                CurrencyEffect: 800m,
                Losses: -640m,
                BaseRatePercent: 13m,
                DividendRatePercent: 13m,
                ExemptionAmount: 4_215m,
                TransactionCount: 42,
                RateSourceText: "Курс: belarusbank, дата 09.05.2026",
                CalculationVersion: "BY-DRAFT-2026.04",
                IsOfflineRate: false,
                OfflineRateMessage: string.Empty,
                Breakdown: rows,
                LegalDisclaimer: "Расчет носит информационный характер. Перед подачей декларации проверьте актуальные правила и официальные источники.");

            return Task.FromResult(model);
        }

        public Task<TaxExportResult> ExportPdfAsync(int year, LegalProfileType profile, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TaxExportResult(true, "PDF-черновик сформирован (design data)."));
        }
    }

    private sealed class DelegateCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
    {
        private readonly Action<object?> _execute = execute;
        private readonly Predicate<object?>? _canExecute = canExecute;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record TaxProfileOption(LegalProfileType Profile, string Title, string Description)
{
    public override string ToString() => Title;
}

public enum TaxBreakdownKind
{
    Income,
    Currency,
    Deduction,
    Benefit,
}

public sealed record TaxScreenReadModel(
    bool IsEmpty,
    string Message,
    string Status,
    string Currency,
    decimal TaxableBase,
    decimal TotalTaxDue,
    decimal TaxSaved,
    decimal RealizedGains,
    decimal Dividends,
    decimal Fees,
    decimal CurrencyEffect,
    decimal Losses,
    decimal BaseRatePercent,
    decimal DividendRatePercent,
    decimal ExemptionAmount,
    int TransactionCount,
    string RateSourceText,
    string CalculationVersion,
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
            "Нет данных",
            "BYN",
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            13m,
            13m,
            2_000m,
            0,
            "Курс: не требуется",
            "tax-draft-v1",
            false,
            string.Empty,
            [],
            "Расчет носит информационный характер. Перед подачей декларации проверьте актуальные правила и официальные источники.");
    }
}

public sealed record TaxBreakdownRow(string Name, decimal Value, string Note, TaxBreakdownKind Kind);

public sealed record TaxBreakdownRowViewModel(string Name, string ValueText, string Note, TaxBreakdownKind Kind);

public sealed record TaxDeadlineRowViewModel(string Month, string Day, string Title, bool IsPrimary);

public sealed record TaxExportResult(bool Succeeded, string Message);
