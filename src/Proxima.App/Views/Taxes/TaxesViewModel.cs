using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Proxima.App.Shell;
using Proxima.App.Notifications;
using Proxima.App.ViewModels;
using Proxima.App.Auth;
using Proxima.Core.Application.Taxes;
using Proxima.App.Common.Commands;

namespace Proxima.App.Views.Taxes;

public enum TaxRecalculationNotificationMode
{
    PageEnter,
    UserAction
}

public sealed class TaxesViewModel : ViewModelBase
{
    private readonly ITaxService _taxService;
    private readonly IShellState _shellState;
    private readonly IRuntimeUserContext _userContext;
    private readonly IAppNotificationCenter _notificationCenter;
    private readonly DelegateCommand _recalculateCommand;
    private readonly DelegateCommand _exportPdfCommand;

    private TaxOverview? _cachedOverview;
    private Guid _cachedPortfolioId;
    private Guid _cachedUserId;
    private int _cachedYear;
    private bool _taxOverviewDirty = true;
    private CancellationTokenSource? _recalculationCts;
    private int _recalculationVersion;

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
    private string _currency = "BYN";
    private string _summaryStatus = "Ожидается";
    private string _rateSourceText = "Источник курса не выбран";
    private string _calculationVersion = "tax-draft-v1";
    private string _currentTaxProfileName = "Физическое лицо";
    private string _currentTaxProfileDescription = "Подтягивается из профиля пользователя";
    private string _taxDisclaimer = "Расчет носит информационный характер и не является юридической консультацией.";

    public TaxesViewModel(
        ITaxService taxService,
        IShellState shellState,
        IRuntimeUserContext userContext,
        IAppNotificationCenter notificationCenter,
        IRuntimeDataInvalidation dataInvalidation)
    {
        _taxService = taxService;
        _shellState = shellState;
        _userContext = userContext;
        _notificationCenter = notificationCenter;

        _shellState.PortfolioChanged += (_, _) => MarkTaxOverviewDirty();
        dataInvalidation.DataInvalidated += (_, _) => MarkTaxOverviewDirty();

        Years = BuildYears(DateTime.UtcNow.Year);
        BreakdownRows = [];
        TaxBreakdownRows = [];

        _recalculateCommand = new DelegateCommand(_ => _ = RecalculateAsync(TaxRecalculationNotificationMode.UserAction, forceRefresh: true), _ => !IsLoading && !IsExporting);
        _exportPdfCommand = new DelegateCommand(_ => _ = ExportPdfAsync(), _ => CanExportPdf);
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
        set
        {
            if (SetProperty(ref _selectedYear, value))
            {
                MarkTaxOverviewDirty();
                OnPropertyChanged(nameof(TaxBreakdownTitle));
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

    public string TaxBreakdownTitle => $"Детализация расчета за {SelectedYear} год";

    public string TaxCalculationNote => $"Профиль: {CurrentTaxProfileName}. Данные берутся из текущего портфеля: сделки, комиссии, дивиденды и удержанные налоги.";

    public async Task RefreshOnPageEnterAsync()
    {
        if (IsLoading || IsExporting)
        {
            return;
        }

        Guid portfolioId = _shellState.CurrentPortfolioId;
        Guid userId = _userContext.UserId;
        int year = SelectedYear;

        if (CanUseCachedOverview(portfolioId, userId, year))
        {
            await ApplyOverviewAsync(_cachedOverview!, TaxRecalculationNotificationMode.PageEnter, notifySuccess: false)
                .ConfigureAwait(true);
            return;
        }

        await RecalculateAsync(TaxRecalculationNotificationMode.PageEnter).ConfigureAwait(true);
    }

    private async Task RecalculateAsync(TaxRecalculationNotificationMode notificationMode, bool forceRefresh = false)
    {
        if (IsLoading && !forceRefresh)
        {
            return;
        }

        Guid portfolioId = _shellState.CurrentPortfolioId;
        Guid userId = _userContext.UserId;
        int year = SelectedYear;

        if (!forceRefresh && CanUseCachedOverview(portfolioId, userId, year))
        {
            await ApplyOverviewAsync(_cachedOverview!, notificationMode, notifySuccess: false).ConfigureAwait(true);
            return;
        }

        _recalculationCts?.Cancel();
        _recalculationCts?.Dispose();
        _recalculationCts = new CancellationTokenSource();
        CancellationToken cancellationToken = _recalculationCts.Token;
        int version = ++_recalculationVersion;

        IsLoading = true;
        HasError = false;
        IsEmpty = false;
        IsOfflineRate = false;
        ExportStatusMessage = string.Empty;
        OfflineRateMessage = string.Empty;

        try
        {
            TaxOverview model = await _taxService
                .GetOverviewAsync(portfolioId, userId, year, cancellationToken)
                .ConfigureAwait(true);

            if (cancellationToken.IsCancellationRequested || version != _recalculationVersion)
            {
                return;
            }

            _cachedOverview = model;
            _cachedPortfolioId = portfolioId;
            _cachedUserId = userId;
            _cachedYear = year;
            _taxOverviewDirty = false;

            await ApplyOverviewAsync(model, notificationMode, notifySuccess: true).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (version != _recalculationVersion)
            {
                return;
            }

            HasError = true;
            StatusMessage = $"Не удалось выполнить расчет: {ex.Message}";
            await _notificationCenter.NotifyAsync(AppNotificationLevel.Error, "Ошибка расчета налогов", StatusMessage, "Налоги").ConfigureAwait(true);
        }
        finally
        {
            if (version == _recalculationVersion)
            {
                IsLoading = false;
            }
        }
    }

    private async Task ApplyOverviewAsync(
        TaxOverview model,
        TaxRecalculationNotificationMode notificationMode,
        bool notifySuccess)
    {
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

        if (notifySuccess && ShouldNotifySuccessfulCalculation(notificationMode, model) && !string.IsNullOrWhiteSpace(model.Message))
        {
            await _notificationCenter.NotifyAsync(
                model.IsOfflineRate ? AppNotificationLevel.Warning : AppNotificationLevel.Success,
                model.IsOfflineRate ? "Налог рассчитан с резервным курсом" : "Налог рассчитан",
                model.Message,
                "Налоги").ConfigureAwait(true);
        }

        OnPropertyChanged(nameof(TaxBreakdownTitle));
        OnPropertyChanged(nameof(TaxCalculationNote));
    }

    private bool CanUseCachedOverview(Guid portfolioId, Guid userId, int year)
    {
        return !_taxOverviewDirty
            && _cachedOverview is not null
            && _cachedPortfolioId == portfolioId
            && _cachedUserId == userId
            && _cachedYear == year;
    }

    private void MarkTaxOverviewDirty()
    {
        _taxOverviewDirty = true;
        _recalculationCts?.Cancel();
    }

    private async Task ExportPdfAsync()
    {
        if (!CanExportPdf)
        {
            ExportStatusMessage = HasError
                ? "Сначала исправьте ошибку расчета налогов."
                : "Сначала выполните расчет налогов по текущему портфелю.";
            await _notificationCenter.NotifyAsync(AppNotificationLevel.Warning, "PDF не сформирован", ExportStatusMessage, "Налоги").ConfigureAwait(true);
            return;
        }

        IsExporting = true;
        ExportStatusMessage = "Формирую PDF-отчет...";
        StatusMessage = ExportStatusMessage;

        try
        {
            TaxExportResult result = await _taxService
                .ExportPdfAsync(_shellState.CurrentPortfolioId, _shellState.CurrentPortfolioName, _userContext.UserId, SelectedYear, CancellationToken.None)
                .ConfigureAwait(true);
            ExportStatusMessage = result.Message;
            StatusMessage = result.Message;
            await _notificationCenter.NotifyAsync(
                result.Succeeded ? AppNotificationLevel.Success : AppNotificationLevel.Error,
                result.Succeeded ? "PDF-отчет сформирован" : "PDF-отчет не сформирован",
                result.Message,
                "Налоги").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            string message = $"Не удалось экспортировать PDF: {ex.Message}";
            ExportStatusMessage = message;
            StatusMessage = message;
            await _notificationCenter.NotifyAsync(AppNotificationLevel.Error, "Ошибка экспорта PDF", message, "Налоги").ConfigureAwait(true);
        }
        finally
        {
            IsExporting = false;
        }
    }


    private static bool ShouldNotifySuccessfulCalculation(TaxRecalculationNotificationMode mode, TaxOverview model)
    {
        if (model.IsEmpty)
        {
            return false;
        }

        return mode == TaxRecalculationNotificationMode.UserAction || model.IsOfflineRate;
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

}
