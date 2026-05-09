using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Proxima.App.Shell;
using Proxima.App.ViewModels;
using Proxima.App.Views.Auth;
using Proxima.Application.Taxes;
using Proxima.Application.Transactions;
using Proxima.Reporting.Reports;

namespace Proxima.App.Views.Taxes;

public sealed class TaxesViewModel : ViewModelBase
{
    private const string BaseCurrency = "BYN";

    private readonly ITaxesReadModelProvider _provider;
    private readonly IShellState _shellState;
    private readonly DelegateCommand _recalculateCommand;
    private readonly DelegateCommand _exportPdfCommand;

    private int _selectedYear = DateTime.UtcNow.Year;
    private bool _isLoading;
    private bool _isExporting;
    private bool _hasError;
    private bool _isEmpty;
    private bool _isOfflineRate;
    private string _statusMessage = "Расчет еще не выполнен.";
    private string _exportStatusMessage = string.Empty;
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
    private decimal _exemptionAmount;
    private decimal _incomeThreshold = 350_000m;
    private int _transactionCount;
    private string _currency = BaseCurrency;
    private string _summaryStatus = "Ожидается";
    private string _rateSourceText = "Источник курса не выбран";
    private string _calculationVersion = "tax-draft-v1";
    private string _currentTaxProfileName = "Физическое лицо";
    private string _currentTaxProfileDescription = "Подтягивается из профиля пользователя";
    private string _taxDisclaimer = "Расчет носит информационный характер и не является юридической консультацией.";

    public TaxesViewModel(ITaxesReadModelProvider provider, IShellState shellState, IRuntimeDataInvalidation dataInvalidation)
    {
        _provider = provider;
        _shellState = shellState;

        Years = BuildYears(DateTime.UtcNow.Year);
        BreakdownRows = [];
        TaxBreakdownRows = [];

        _recalculateCommand = new DelegateCommand(_ => _ = RecalculateAsync(), _ => !IsLoading && !IsExporting);
        _exportPdfCommand = new DelegateCommand(_ => _ = ExportPdfAsync(), _ => CanExportPdf);
        _shellState.PortfolioChanged += (_, _) => _ = RecalculateAsync();
        dataInvalidation.DataInvalidated += (_, _) => _ = RecalculateAsync();

        _ = RecalculateAsync();
    }

    public string Title => "Налоги";

    public string Subtitle => "Расчет по текущему портфелю с курсами НБРБ/Belarusbank и налоговым профилем из страницы профиля.";

    public IReadOnlyList<int> Years { get; }

    public ObservableCollection<TaxBreakdownRowViewModel> BreakdownRows { get; }

    public ObservableCollection<TaxBreakdownRowViewModel> TaxBreakdownRows { get; }

    public ICommand RecalculateCommand => _recalculateCommand;

    public ICommand ExportPdfCommand => _exportPdfCommand;

    public int SelectedYear
    {
        get => _selectedYear;
        set => SetProperty(ref _selectedYear, value);
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

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
            {
                OnPropertyChanged(nameof(CanExportPdf));
                OnPropertyChanged(nameof(ExportButtonText));
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

    public bool CanExportPdf => HasContent && !IsExporting;

    public string ExportButtonText => IsExporting ? "Формирование PDF..." : "Экспортировать отчёт";

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

    public string ExportStatusMessage
    {
        get => _exportStatusMessage;
        private set => SetProperty(ref _exportStatusMessage, value);
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

    public decimal IncomeThreshold
    {
        get => _incomeThreshold;
        private set
        {
            if (SetProperty(ref _incomeThreshold, value))
            {
                OnPropertyChanged(nameof(IncomeThresholdProgress));
                OnPropertyChanged(nameof(IncomeThresholdProgressText));
                OnPropertyChanged(nameof(IncomeThresholdCaption));
                OnPropertyChanged(nameof(IncomeThresholdRightLabel));
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
                OnPropertyChanged(nameof(IncomeThresholdRightLabel));
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

    public string CurrentTaxProfileName
    {
        get => _currentTaxProfileName;
        private set => SetProperty(ref _currentTaxProfileName, value);
    }

    public string CurrentTaxProfileDescription
    {
        get => _currentTaxProfileDescription;
        private set => SetProperty(ref _currentTaxProfileDescription, value);
    }

    public string TaxDisclaimer
    {
        get => _taxDisclaimer;
        private set => SetProperty(ref _taxDisclaimer, value);
    }

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

    public string IncomeThresholdCaption => IncomeThreshold <= 0m
        ? "Лимит не применяется"
        : $"{FormatMoney(TaxableBase, Currency, compact: true)} / {FormatMoney(IncomeThreshold, Currency, compact: true)}";

    public string IncomeThresholdRightLabel => IncomeThreshold <= 0m ? "без лимита" : FormatMoney(IncomeThreshold, Currency, compact: true);

    public string TransactionCountText => TransactionCount == 1 ? "1 операция" : $"{TransactionCount} операций";

    public string TaxBreakdownTitle => $"Tax breakdown {SelectedYear}";

    public string TaxCalculationNote => $"Профиль: {CurrentTaxProfileName}. Данные берутся из текущего портфеля: сделки, комиссии, дивиденды и удержанные налоги.";

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
        ExportStatusMessage = string.Empty;
        OfflineRateMessage = string.Empty;

        try
        {
            TaxScreenReadModel model = await _provider.GetAsync(SelectedYear, CancellationToken.None).ConfigureAwait(true);

            BreakdownRows.Clear();
            foreach (TaxBreakdownRow row in model.Breakdown)
            {
                BreakdownRows.Add(new TaxBreakdownRowViewModel(row.Name, FormatMoney(row.Value, model.Currency), row.Note, row.Kind));
            }

            TaxBreakdownRows.Clear();
            foreach (TaxBreakdownRow row in model.TaxBreakdown)
            {
                TaxBreakdownRows.Add(new TaxBreakdownRowViewModel(row.Name, row.Kind == TaxBreakdownKind.Info ? "—" : FormatMoney(row.Value, model.Currency), row.Note, row.Kind));
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
            IncomeThreshold = model.IncomeThreshold;
            TransactionCount = model.TransactionCount;
            SummaryStatus = model.Status;
            StatusMessage = model.Message;
            RateSourceText = model.RateSourceText;
            CalculationVersion = model.CalculationVersion;
            CurrentTaxProfileName = model.ProfileName;
            CurrentTaxProfileDescription = model.ProfileDescription;
            TaxDisclaimer = model.LegalDisclaimer;
            IsEmpty = model.IsEmpty;
            IsOfflineRate = model.IsOfflineRate;
            OfflineRateMessage = model.OfflineRateMessage;

            OnPropertyChanged(nameof(TaxBreakdownTitle));
            OnPropertyChanged(nameof(TaxCalculationNote));
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
        if (!CanExportPdf)
        {
            ExportStatusMessage = HasError
                ? "Сначала исправьте ошибку расчета налогов."
                : "Сначала выполните расчет налогов по текущему портфелю.";
            return;
        }

        IsExporting = true;
        ExportStatusMessage = "Формирую PDF-отчет...";
        StatusMessage = ExportStatusMessage;

        try
        {
            TaxExportResult result = await _provider.ExportPdfAsync(SelectedYear, CancellationToken.None).ConfigureAwait(true);
            ExportStatusMessage = result.Message;
            StatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            string message = $"Не удалось экспортировать PDF: {ex.Message}";
            ExportStatusMessage = message;
            StatusMessage = message;
        }
        finally
        {
            IsExporting = false;
        }
    }

    private void OnContentStateChanged()
    {
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(CanExportPdf));
        _exportPdfCommand.RaiseCanExecuteChanged();
    }

    private static IReadOnlyList<int> BuildYears(int currentYear)
    {
        return [currentYear, currentYear - 1, currentYear - 2, currentYear - 3];
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
        Task<TaxScreenReadModel> GetAsync(int year, CancellationToken cancellationToken);

        Task<TaxExportResult> ExportPdfAsync(int year, CancellationToken cancellationToken);
    }

    public sealed class AppTaxesReadModelProvider : ITaxesReadModelProvider
    {
        private readonly ITransactionService _transactions;
        private readonly ITaxCalculator _taxCalculator;
        private readonly IReportService _reportService;
        private readonly IShellState _shellState;
        private readonly IRuntimeUserContext _userContext;

        public AppTaxesReadModelProvider(
            ITransactionService transactions,
            ITaxCalculator taxCalculator,
            IReportService reportService,
            IShellState shellState,
            IRuntimeUserContext userContext)
        {
            _transactions = transactions;
            _taxCalculator = taxCalculator;
            _reportService = reportService;
            _shellState = shellState;
            _userContext = userContext;
        }

        public async Task<TaxScreenReadModel> GetAsync(int year, CancellationToken cancellationToken)
        {
            LegalProfileType profile = ResolveProfile(_userContext.UserId);
            TaxProfileDescriptor descriptor = DescribeProfile(profile);

            IReadOnlyList<Proxima.Domain.Transactions.PortfolioTransaction> transactions = await _transactions
                .ListActiveAsync(_shellState.CurrentPortfolioId, cancellationToken)
                .ConfigureAwait(false);

            if (transactions.Count == 0)
            {
                return TaxScreenReadModel.Empty("Нет транзакций для расчета налогов. Добавьте сделки, дивиденды или комиссии в портфель.", descriptor);
            }

            List<TaxTransactionSnapshot> snapshots = transactions
                .Select(item => new TaxTransactionSnapshot(
                    item.AssetId,
                    item.TradeDate,
                    item.Type,
                    item.Quantity,
                    item.Price,
                    item.GrossAmount,
                    item.FeeAmount,
                    item.TaxAmount,
                    item.Currency))
                .ToList();

            int yearTransactionCount = snapshots.Count(item => item.TradeDate.Year == year);
            if (yearTransactionCount == 0)
            {
                return TaxScreenReadModel.Empty($"За {year} год нет транзакций для расчета налогов.", descriptor);
            }

            TaxCalculationResult calculation = await _taxCalculator
                .CalculateAsync(snapshots, year, profile, BaseCurrency, cancellationToken)
                .ConfigureAwait(false);

            bool offlineRate = !calculation.Succeeded || calculation.RateSource.Contains("mock", StringComparison.OrdinalIgnoreCase);
            string offlineRateMessage = offlineRate
                ? "НБРБ и Belarusbank не вернули курс для части операций. В расчете использован fallback — перепроверьте суммы перед подачей декларации."
                : string.Empty;

            IReadOnlyList<TaxBreakdownRow> rows = BuildBreakdownRows(calculation);
            IReadOnlyList<TaxBreakdownRow> taxRows = BuildTaxBreakdownRows(calculation, descriptor);

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
                IncomeThreshold: calculation.RuleSet.FirstThreshold,
                TransactionCount: yearTransactionCount,
                RateSourceText: rateText,
                CalculationVersion: calculation.RuleSet.Version,
                ProfileName: descriptor.Name,
                ProfileDescription: descriptor.Description,
                IsOfflineRate: offlineRate,
                OfflineRateMessage: offlineRateMessage,
                Breakdown: rows,
                TaxBreakdown: taxRows,
                LegalDisclaimer: calculation.RuleSet.Disclaimer);
        }

        public async Task<TaxExportResult> ExportPdfAsync(int year, CancellationToken cancellationToken)
        {
            TaxScreenReadModel model = await GetAsync(year, cancellationToken).ConfigureAwait(false);
            if (model.IsEmpty)
            {
                return new TaxExportResult(false, model.Message);
            }

            string portfolioName = string.IsNullOrWhiteSpace(_shellState.CurrentPortfolioName)
                ? "Основной портфель"
                : _shellState.CurrentPortfolioName;

            TaxReportRequest request = new(
                UserDisplayName: portfolioName,
                TaxProfile: model.ProfileName,
                TaxProfileDescription: model.ProfileDescription,
                Year: year,
                TaxableBase: model.TaxableBase,
                TotalTaxDue: model.TotalTaxDue,
                TaxSaved: model.TaxSaved,
                Dividends: model.Dividends,
                RealizedGains: model.RealizedGains,
                Fees: model.Fees,
                Losses: model.Losses,
                CurrencyEffect: model.CurrencyEffect,
                BaseRatePercent: model.BaseRatePercent,
                DividendRatePercent: model.DividendRatePercent,
                IncomeThreshold: model.IncomeThreshold,
                TransactionCount: model.TransactionCount,
                Currency: model.Currency,
                ExchangeRateNotes: model.RateSourceText,
                CalculationVersion: model.CalculationVersion,
                LegalDisclaimer: model.LegalDisclaimer,
                CalculationBreakdown: model.Breakdown.Select(ToReportLine).ToList(),
                TaxBreakdown: model.TaxBreakdown.Select(ToReportLine).ToList(),
                OutputDirectory: ProximaReportingComposition.GetDefaultReportDirectory());

            ReportExportResult export = await _reportService.ExportTaxPdfAsync(request, cancellationToken).ConfigureAwait(false);
            string message = export.Succeeded
                ? $"Отчет сохранен: {export.OutputPath}"
                : export.Message;

            return new TaxExportResult(export.Succeeded, message);
        }


        private static TaxReportLine ToReportLine(TaxBreakdownRow row)
        {
            return new TaxReportLine(row.Name, row.Value, row.Note, row.Kind.ToString());
        }

        private static IReadOnlyList<TaxBreakdownRow> BuildBreakdownRows(TaxCalculationResult calculation)
        {
            return
            [
                new TaxBreakdownRow("Реализованная прибыль", calculation.RealizedGains, "FIFO-оценка продаж активов за выбранный год", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Дивиденды и доходы", calculation.Dividends, "Дивиденды, купоны, airdrop и staking reward", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Курсовая разница", calculation.CurrencyEffect, "Конвертация операций в BYN по курсу на дату операции", TaxBreakdownKind.Currency),
                new TaxBreakdownRow("Комиссии и удержания", -calculation.Fees, "Комиссии брокера и удержанный за рубежом налог", TaxBreakdownKind.Deduction),
                new TaxBreakdownRow("Льготы / зачет", calculation.TaxSaved, "Зачет иностранного налога и расходы текущего периода", TaxBreakdownKind.Benefit),
                new TaxBreakdownRow("Убытки текущего года", calculation.Losses, "Отрицательный результат продаж, уменьшающий базу в черновике", TaxBreakdownKind.Deduction)
            ];
        }

        private static IReadOnlyList<TaxBreakdownRow> BuildTaxBreakdownRows(TaxCalculationResult calculation, TaxProfileDescriptor descriptor)
        {
            decimal grossTaxBeforeBenefits = calculation.TaxDue + calculation.TaxSaved;
            return
            [
                new TaxBreakdownRow("Профиль", 0m, descriptor.Name, TaxBreakdownKind.Info),
                new TaxBreakdownRow("База", calculation.TaxableBase, descriptor.TaxBaseNote, TaxBreakdownKind.Income),
                new TaxBreakdownRow("Налог до зачета", grossTaxBeforeBenefits, "Оценка по ставкам выбранного профиля", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Зачет / льготы", -calculation.TaxSaved, "Удержанный иностранный налог и допустимые расходы", TaxBreakdownKind.Benefit),
                new TaxBreakdownRow("К уплате", calculation.TaxDue, "Итоговая оценка налога по текущему портфелю", TaxBreakdownKind.Income)
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

        private static LegalProfileType ResolveProfile(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return LegalProfileType.PhysicalPerson;
            }

            string path = Path.Combine(GetProfileExtrasDirectory(), $"{userId:N}.legal-profile");
            if (!File.Exists(path))
            {
                return LegalProfileType.PhysicalPerson;
            }

            string raw = File.ReadAllText(path).Trim();
            return raw switch
            {
                "PhysicalPerson" => LegalProfileType.PhysicalPerson,
                "SelfEmployed" => LegalProfileType.SelfEmployed,
                "SoleProprietor" => LegalProfileType.IndividualEntrepreneur,
                "Company" => LegalProfileType.LLC,
                _ => LegalProfileType.PhysicalPerson,
            };
        }

        private static TaxProfileDescriptor DescribeProfile(LegalProfileType profile)
        {
            return profile switch
            {
                LegalProfileType.SelfEmployed => new TaxProfileDescriptor("Самозанятый", "Налог на профессиональный доход: 10%, затем 20% после 60 000 BYN.", "Профессиональный доход, рассчитанный по поступлениям портфеля."),
                LegalProfileType.IndividualEntrepreneur => new TaxProfileDescriptor("Индивидуальный предприниматель", "Подоходный налог ИП: 20%, повышенная зона после 500 000 BYN.", "Предпринимательский доход за вычетом расходов и убытков."),
                LegalProfileType.LLC or LegalProfileType.JSC => new TaxProfileDescriptor("ООО", "Налог на прибыль организаций: базовая ставка 20%.", "Прибыль организации от операций портфеля."),
                _ => new TaxProfileDescriptor("Физическое лицо", "Подоходный налог: 13%, 25% после 350 000 BYN, 30% после 600 000 BYN.", "Инвестиционный доход физического лица-резидента РБ."),
            };
        }

        private static string GetProfileExtrasDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Proxima",
                "Profile");
        }
    }

    private sealed class DesignTaxesReadModelProvider : ITaxesReadModelProvider
    {
        public Task<TaxScreenReadModel> GetAsync(int year, CancellationToken cancellationToken)
        {
            TaxProfileDescriptor descriptor = new("Физическое лицо", "Подоходный налог: 13%, 25% после 350 000 BYN, 30% после 600 000 BYN.", "Инвестиционный доход физического лица-резидента РБ.");
            IReadOnlyList<TaxBreakdownRow> rows =
            [
                new TaxBreakdownRow("Реализованная прибыль", 97_850m, "FIFO-оценка продаж активов за выбранный год", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Дивиденды и доходы", 11_200m, "Дивиденды, купоны, airdrop и staking reward", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Курсовая разница", 800m, "Конвертация операций в BYN", TaxBreakdownKind.Currency),
                new TaxBreakdownRow("Комиссии и удержания", -1_420m, "Комиссии брокера и удержанные налоги", TaxBreakdownKind.Deduction),
                new TaxBreakdownRow("Льготы / зачет", 4_215m, "Зачет иностранного налога и расходы", TaxBreakdownKind.Benefit),
                new TaxBreakdownRow("Убытки текущего года", -640m, "Отрицательный результат продаж", TaxBreakdownKind.Deduction)
            ];

            IReadOnlyList<TaxBreakdownRow> taxRows =
            [
                new TaxBreakdownRow("Профиль", 0m, descriptor.Name, TaxBreakdownKind.Info),
                new TaxBreakdownRow("База", 109_850m, descriptor.TaxBaseNote, TaxBreakdownKind.Income),
                new TaxBreakdownRow("Налог до зачета", 18_495.50m, "Оценка по ставкам выбранного профиля", TaxBreakdownKind.Income),
                new TaxBreakdownRow("Зачет / льготы", -4_215m, "Удержанный иностранный налог и расходы", TaxBreakdownKind.Benefit),
                new TaxBreakdownRow("К уплате", 14_280.50m, "Итоговая оценка налога", TaxBreakdownKind.Income)
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
                Fees: 1_420m,
                CurrencyEffect: 800m,
                Losses: -640m,
                BaseRatePercent: 13m,
                DividendRatePercent: 13m,
                ExemptionAmount: 0m,
                IncomeThreshold: 350_000m,
                TransactionCount: 42,
                RateSourceText: "Курс: nbrb, дата 09.05.2026",
                CalculationVersion: "BY-PIT-2026.01",
                ProfileName: descriptor.Name,
                ProfileDescription: descriptor.Description,
                IsOfflineRate: false,
                OfflineRateMessage: string.Empty,
                Breakdown: rows,
                TaxBreakdown: taxRows,
                LegalDisclaimer: "Расчет носит информационный характер и не является юридической консультацией.");

            return Task.FromResult(model);
        }

        public Task<TaxExportResult> ExportPdfAsync(int year, CancellationToken cancellationToken)
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

public enum TaxBreakdownKind
{
    Income,
    Currency,
    Deduction,
    Benefit,
    Info,
}

public sealed record TaxProfileDescriptor(string Name, string Description, string TaxBaseNote);

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
    decimal IncomeThreshold,
    int TransactionCount,
    string RateSourceText,
    string CalculationVersion,
    string ProfileName,
    string ProfileDescription,
    bool IsOfflineRate,
    string OfflineRateMessage,
    IReadOnlyList<TaxBreakdownRow> Breakdown,
    IReadOnlyList<TaxBreakdownRow> TaxBreakdown,
    string LegalDisclaimer)
{
    public static TaxScreenReadModel Empty(string message, TaxProfileDescriptor descriptor)
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
            0m,
            350_000m,
            0,
            "Курс: не требуется",
            "tax-draft-v1",
            descriptor.Name,
            descriptor.Description,
            false,
            string.Empty,
            [],
            [],
            "Расчет носит информационный характер и не является юридической консультацией.");
    }
}

public sealed record TaxBreakdownRow(string Name, decimal Value, string Note, TaxBreakdownKind Kind);

public sealed record TaxBreakdownRowViewModel(string Name, string ValueText, string Note, TaxBreakdownKind Kind);

public sealed record TaxExportResult(bool Succeeded, string Message);
