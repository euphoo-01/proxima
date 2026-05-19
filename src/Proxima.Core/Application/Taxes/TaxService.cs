using System.Globalization;
using Proxima.Core.Application.Auth;
using Proxima.Core.Application.Reporting;
using Proxima.Core.Application.Transactions;
using Proxima.Core.Domain.Auth;
using Proxima.Core.Domain.Transactions;

namespace Proxima.Core.Application.Taxes;

public sealed class TaxService(
    ITransactionService transactions,
    ITaxCalculator taxCalculator,
    IReportService reportService,
    ILocalUserRepository localUsers) : ITaxService
{
    private const string BaseCurrency = "BYN";

    public async Task<TaxOverview> GetOverviewAsync(
        Guid portfolioId,
        Guid userId,
        int year,
        CancellationToken cancellationToken = default)
    {
        LegalProfileType profile = await ResolveProfileAsync(userId, cancellationToken).ConfigureAwait(false);
        TaxProfileDescriptor descriptor = DescribeProfile(profile);

        IReadOnlyList<PortfolioTransaction> portfolioTransactions = await transactions
            .ListActiveAsync(portfolioId, cancellationToken)
            .ConfigureAwait(false);

        if (portfolioTransactions.Count == 0)
        {
            return TaxOverview.Empty("Нет транзакций для расчета налогов. Добавьте сделки, дивиденды или комиссии в портфель.", descriptor);
        }

        DateTimeOffset reportYearEnd = new(year, 12, 31, 23, 59, 59, TimeSpan.Zero);
        List<PortfolioTransaction> calculationTransactions = portfolioTransactions
            .Where(item => item.TradeDate <= reportYearEnd)
            .ToList();

        if (calculationTransactions.Count == 0)
        {
            return TaxOverview.Empty($"До конца {year} года нет транзакций для расчета налогов.", descriptor);
        }

        List<TaxTransactionSnapshot> snapshots = calculationTransactions
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
            return TaxOverview.Empty($"За {year} год нет транзакций для расчета налогов.", descriptor);
        }

        TaxCalculationResult calculation = await taxCalculator
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

        return new TaxOverview(
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

    public async Task<TaxExportResult> ExportPdfAsync(
        Guid portfolioId,
        string portfolioName,
        Guid userId,
        int year,
        CancellationToken cancellationToken = default)
    {
        TaxOverview model = await GetOverviewAsync(portfolioId, userId, year, cancellationToken).ConfigureAwait(false);
        if (model.IsEmpty)
        {
            return new TaxExportResult(false, model.Message);
        }

        string displayPortfolioName = string.IsNullOrWhiteSpace(portfolioName)
            ? "Основной портфель"
            : portfolioName;

        TaxReportRequest request = new(
            UserDisplayName: displayPortfolioName,
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
            OutputDirectory: string.Empty);

        ReportExportResult export = await reportService.ExportTaxPdfAsync(request, cancellationToken).ConfigureAwait(false);
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

    private async Task<LegalProfileType> ResolveProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return LegalProfileType.PhysicalPerson;
        }

        LocalUserProfile? profile = await localUsers
            .FindByIdAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return profile?.LegalProfile switch
        {
            LegalProfileKind.SelfEmployed => LegalProfileType.SelfEmployed,
            LegalProfileKind.SoleProprietor => LegalProfileType.IndividualEntrepreneur,
            LegalProfileKind.Company => LegalProfileType.LLC,
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

}
