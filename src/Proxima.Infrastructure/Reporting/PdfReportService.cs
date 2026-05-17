using Proxima.Core.Application.Reporting;
using System.Globalization;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Proxima.Infrastructure.Reporting;

public sealed class PdfReportService : IReportService
{
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly object FontResolverLock = new();

    public ReportPreviewResult PreviewPortfolio(PortfolioReportRequest request)
    {
        PortfolioReportRequest safeRequest = NormalizePortfolioRequest(request);
        if (!ValidateOutputDirectory(safeRequest.OutputDirectory, out string message))
        {
            return new ReportPreviewResult(false, message, []);
        }

        List<string> sections =
        [
            "Portfolio Summary",
            "Allocation",
            "Allocation chart",
            "Top Assets",
            "Portfolio Metrics",
            "Asset table",
            "Disclaimer",
        ];

        return new ReportPreviewResult(true, "Preview ready.", sections);
    }

    public ReportPreviewResult PreviewTax(TaxReportRequest request)
    {
        if (!ValidateOutputDirectory(request.OutputDirectory, out string message))
        {
            return new ReportPreviewResult(false, message, []);
        }

        List<string> sections =
        [
            "Summary cards",
            "Taxpayer profile",
            "Calculation breakdown",
            "Exchange rate notes",
            "Legal disclaimer",
        ];

        return new ReportPreviewResult(true, "Preview ready.", sections);
    }

    public async Task<ReportExportResult> ExportPortfolioPdfAsync(PortfolioReportRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return new ReportExportResult(false, "Portfolio export failed: request is empty.", null);
        }

        PortfolioReportRequest safeRequest = NormalizePortfolioRequest(request);
        if (!ValidateOutputDirectory(safeRequest.OutputDirectory, out string message))
        {
            return new ReportExportResult(false, message, null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(safeRequest.OutputDirectory);
            string path = Path.Combine(safeRequest.OutputDirectory, BuildFileName("portfolio-report", safeRequest.PortfolioName));

            EnsureFontResolver();
            using PdfDocument document = new();
            document.Info.Title = "Proxima Portfolio Report";
            document.Info.Author = "Proxima";
            document.Info.Subject = safeRequest.PortfolioName;
            document.Info.CreationDate = DateTime.Now;

            ReportCanvas canvas = new(document, "Отчет по портфелю", safeRequest.PortfolioName);
            canvas.DrawTitle("Отчет по текущему портфелю", safeRequest.PeriodLabel);
            canvas.DrawKpiCards([
                new KpiCard("Стоимость портфеля", FormatMoney(safeRequest.TotalValue, safeRequest.Currency), "Все активы в базовой валюте", ReportPalette.BlueTint),
                new KpiCard("P&L", FormatMoney(safeRequest.ProfitLoss, safeRequest.Currency), "Финансовый результат", safeRequest.ProfitLoss >= 0m ? ReportPalette.GreenTint : ReportPalette.RedTint),
                new KpiCard("Операции", safeRequest.TransactionCount.ToString(CultureInfo.InvariantCulture), "Количество транзакций", ReportPalette.GrayTint),
            ]);

            canvas.DrawSectionTitle("Основные показатели");
            canvas.DrawSimpleRows(safeRequest.RiskMetrics.Select(item => new SimpleRow(item.Metric, item.Value, "Показатель портфеля")).ToList());

            canvas.DrawSectionTitle("Распределение портфеля");
            canvas.DrawSimpleRows(safeRequest.Allocation.Select(item => new SimpleRow(item.Category, FormatMoney(item.Value, safeRequest.Currency), "Категория портфеля")).ToList());
            canvas.DrawHorizontalBars(safeRequest.Allocation, safeRequest.Currency);

            canvas.DrawSectionTitle("Крупнейшие активы");
            canvas.DrawSimpleRows(safeRequest.TopAssets.Select(item => new SimpleRow(item.Asset, FormatMoney(item.Value, safeRequest.Currency), "Текущая оценка")).ToList());
            canvas.DrawHorizontalBars(safeRequest.TopAssets, safeRequest.Currency);

            canvas.DrawAssetTable(safeRequest.AssetRows ?? [], safeRequest.Currency);
            canvas.DrawDisclaimer(safeRequest.Disclaimer);
            canvas.Save(path);
            await Task.CompletedTask.ConfigureAwait(false);
            return new ReportExportResult(true, "Portfolio report exported.", path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Exception root = ex.GetBaseException();
            string rootMessage = string.IsNullOrWhiteSpace(root.Message) ? root.GetType().Name : root.Message;
            return new ReportExportResult(false, $"Portfolio export failed: {root.GetType().Name}: {rootMessage}", null);
        }
    }

    public async Task<ReportExportResult> ExportTaxPdfAsync(TaxReportRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return new ReportExportResult(false, "Tax export failed: request is empty.", null);
        }

        TaxReportRequest safeRequest = NormalizeTaxRequest(request);

        if (!ValidateOutputDirectory(safeRequest.OutputDirectory, out string message))
        {
            return new ReportExportResult(false, message, null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(safeRequest.OutputDirectory);
            string path = Path.Combine(safeRequest.OutputDirectory, BuildFileName("tax-report", $"{safeRequest.UserDisplayName}-{safeRequest.Year}"));

            EnsureFontResolver();
            using PdfDocument document = new();
            document.Info.Title = $"Proxima Tax Report {safeRequest.Year}";
            document.Info.Author = "Proxima";
            document.Info.Subject = safeRequest.UserDisplayName;
            document.Info.CreationDate = DateTime.Now;

            ReportCanvas canvas = new(document, "Налоговый отчет", safeRequest.UserDisplayName);
            canvas.DrawTitle($"Налоговый отчет за {safeRequest.Year} год", "Черновик расчета по текущему портфелю");
            canvas.DrawKpiCards([
                new KpiCard("Всего налогов", FormatMoney(safeRequest.TotalTaxDue, safeRequest.Currency), "Оценка суммы к уплате", ReportPalette.BlueTint),
                new KpiCard("Облагаемая база", FormatMoney(safeRequest.TaxableBase, safeRequest.Currency), "После учета расходов и убытков", ReportPalette.GrayTint),
                new KpiCard("Льготы / зачет", FormatMoney(safeRequest.TaxSaved, safeRequest.Currency), "Иностранный налог и допустимые расходы", ReportPalette.GreenTint),
            ]);

            canvas.DrawInfoGrid([
                new InfoCell("Портфель", safeRequest.UserDisplayName),
                new InfoCell("Профиль", safeRequest.TaxProfile),
                new InfoCell("Ставка РБ", $"{safeRequest.BaseRatePercent:0.##}%"),
                new InfoCell("Дивиденды", $"{safeRequest.DividendRatePercent:0.##}%"),
                new InfoCell("Порог дохода", safeRequest.IncomeThreshold <= 0m ? "не применяется" : FormatMoney(safeRequest.IncomeThreshold, safeRequest.Currency)),
                new InfoCell("Операции", safeRequest.TransactionCount.ToString(CultureInfo.InvariantCulture)),
                new InfoCell("Версия расчета", safeRequest.CalculationVersion),
                new InfoCell("Курсы валют", safeRequest.ExchangeRateNotes),
            ]);

            canvas.DrawSectionTitle("Финансовая база");
            canvas.DrawSimpleRows([
                new SimpleRow("Реализованная прибыль", FormatMoney(safeRequest.RealizedGains, safeRequest.Currency), "Прибыль по закрытым сделкам"),
                new SimpleRow("Дивиденды", FormatMoney(safeRequest.Dividends, safeRequest.Currency), "Доход по дивидендам"),
                new SimpleRow("Комиссии", FormatMoney(safeRequest.Fees, safeRequest.Currency), "Расходы брокера и биржи"),
                new SimpleRow("Курсовая разница", FormatMoney(safeRequest.CurrencyEffect, safeRequest.Currency), "Эффект пересчета валют"),
                new SimpleRow("Убытки", FormatMoney(safeRequest.Losses, safeRequest.Currency), "Учитываемые убытки периода"),
            ]);

            canvas.DrawCallout("Налоговый профиль", safeRequest.TaxProfileDescription, ReportPalette.BlueTint);
            canvas.DrawTaxRows("Детализация расчета", safeRequest.CalculationBreakdown, safeRequest.Currency);
            canvas.DrawTaxRows("Разбор налога", safeRequest.TaxBreakdown, safeRequest.Currency);
            canvas.DrawRateNote(safeRequest.ExchangeRateNotes);
            canvas.DrawDisclaimer(safeRequest.LegalDisclaimer);
            canvas.Save(path);

            await Task.CompletedTask.ConfigureAwait(false);
            return new ReportExportResult(true, "Tax report exported.", path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Exception root = ex.GetBaseException();
            string rootMessage = string.IsNullOrWhiteSpace(root.Message) ? root.GetType().Name : root.Message;
            return new ReportExportResult(false, $"Tax export failed: {root.GetType().Name}: {rootMessage}", null);
        }
    }


    private static string TrimToFit(string value, double width, double fontSize)
    {
        int maxChars = Math.Max(12, Convert.ToInt32(width / Math.Max(4d, fontSize * 0.45d)));
        return value.Length <= maxChars ? value : value[..Math.Max(1, maxChars - 1)] + "…";
    }

    private static void EnsureFontResolver()
    {
        lock (FontResolverLock)
        {
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new ProximaFontResolver();
            }
        }
    }

    private static string BuildFileName(string prefix, string? identity)
    {
        identity = SafeText(identity, "report");
        string safe = new string(identity.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "report";
        }

        return $"{prefix}-{safe}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
    }

    private static bool ValidateOutputDirectory(string outputDirectory, out string message)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            message = "Output directory is required.";
            return false;
        }

        message = string.Empty;
        return true;
    }


    private static PortfolioReportRequest NormalizePortfolioRequest(PortfolioReportRequest request)
    {
        IReadOnlyList<(string Category, decimal Value)> allocation = request.Allocation ?? Array.Empty<(string Category, decimal Value)>();
        IReadOnlyList<(string Asset, decimal Value)> topAssets = request.TopAssets ?? Array.Empty<(string Asset, decimal Value)>();
        IReadOnlyList<(string Metric, string Value)> riskMetrics = request.RiskMetrics ?? Array.Empty<(string Metric, string Value)>();

        return request with
        {
            PortfolioName = SafeText(request.PortfolioName, "Основной портфель"),
            PeriodLabel = SafeText(request.PeriodLabel, $"Срез на {DateTime.Now:dd.MM.yyyy}"),
            Currency = SafeText(request.Currency, "USD"),
            Disclaimer = SafeText(request.Disclaimer, "Отчет носит информационный характер."),
            Allocation = allocation,
            TopAssets = topAssets,
            RiskMetrics = riskMetrics,
            AssetRows = request.AssetRows ?? Array.Empty<PortfolioReportAsset>(),
            OutputDirectory = SafeText(request.OutputDirectory, ReportPathDefaults.GetDefaultReportDirectory()),
        };
    }

    private static TaxReportRequest NormalizeTaxRequest(TaxReportRequest request)
    {
        return request with
        {
            UserDisplayName = SafeText(request.UserDisplayName, "Основной портфель"),
            TaxProfile = SafeText(request.TaxProfile, "Физическое лицо"),
            TaxProfileDescription = SafeText(request.TaxProfileDescription, "Описание налогового профиля недоступно."),
            Currency = SafeText(request.Currency, "BYN"),
            ExchangeRateNotes = SafeText(request.ExchangeRateNotes, "Информация о курсах валют недоступна."),
            CalculationVersion = SafeText(request.CalculationVersion, "tax-draft-v1"),
            LegalDisclaimer = SafeText(request.LegalDisclaimer, "Расчет носит информационный характер и не является юридической консультацией."),
            CalculationBreakdown = request.CalculationBreakdown ?? Array.Empty<TaxReportLine>(),
            TaxBreakdown = request.TaxBreakdown ?? Array.Empty<TaxReportLine>(),
            OutputDirectory = SafeText(request.OutputDirectory, ReportPathDefaults.GetDefaultReportDirectory()),
        };
    }

    private static string SafeText(string? value, string fallback = "—")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : NormalizePdfText(value.Trim());
    }

    private static string NormalizePdfText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Replace("\u00A0", " ", StringComparison.Ordinal);
    }

    private static string FormatMoney(decimal value, string currency)
    {
        return $"{value.ToString("N2", MoneyCulture)} {SafeText(currency, "BYN")}";
    }

    private sealed class ReportCanvas
    {
        private const double PageWidth = 595d;
        private const double PageHeight = 842d;
        private const double Margin = 42d;
        private const double FooterY = 803d;
        private const string FontFamily = ProximaFontResolver.ReportFontFamily;

        private readonly PdfDocument _document;
        private readonly string _reportName;
        private readonly string _contextName;
        private readonly XFont _brandFont = new(FontFamily, 17, XFontStyleEx.Bold);
        private readonly XFont _titleFont = new(FontFamily, 27, XFontStyleEx.Bold);
        private readonly XFont _sectionFont = new(FontFamily, 15, XFontStyleEx.Bold);
        private readonly XFont _kpiLabelFont = new(FontFamily, 9, XFontStyleEx.Regular);
        private readonly XFont _kpiValueFont = new(FontFamily, 19, XFontStyleEx.Bold);
        private readonly XFont _bodyFont = new(FontFamily, 9.5, XFontStyleEx.Regular);
        private readonly XFont _bodyBoldFont = new(FontFamily, 9.5, XFontStyleEx.Bold);
        private readonly XFont _smallFont = new(FontFamily, 8, XFontStyleEx.Regular);
        private readonly XFont _footerFont = new(FontFamily, 7.5, XFontStyleEx.Regular);

        private PdfPage _page;
        private XGraphics _gfx;
        private double _y;
        private int _pageNumber;

        public ReportCanvas(PdfDocument document, string reportName, string contextName)
        {
            _document = document;
            _reportName = SafeText(reportName, "Отчет");
            _contextName = SafeText(contextName, "Портфель");
            _page = CreatePage();
            _gfx = XGraphics.FromPdfPage(_page);
            _y = Margin;
            _pageNumber = 1;
            DrawPageHeader();
        }

        public void Save(string path)
        {
            DrawFooter();
            _gfx.Dispose();
            _document.Save(path);
        }

        public void DrawTitle(string title, string subtitle)
        {
            EnsureSpace(90d);
            _gfx.DrawString(SafeText(title, "Отчет"), _titleFont, ReportPalette.Navy, new XRect(Margin, _y, PageWidth - Margin * 2d, 32d), XStringFormats.TopLeft);
            _y += 37d;
            DrawText(subtitle, _bodyFont, ReportPalette.Muted, new XRect(Margin, _y, PageWidth - Margin * 2d, 30d));
            _y += 48d;
        }

        public void DrawKpiCards(IReadOnlyList<KpiCard> cards)
        {
            EnsureSpace(118d);
            double gap = 14d;
            double width = (PageWidth - Margin * 2d - gap * 2d) / 3d;
            double startY = _y;

            for (int i = 0; i < cards.Count && i < 3; i++)
            {
                KpiCard card = cards[i];
                double x = Margin + i * (width + gap);
                DrawRoundedRect(x, startY, width, 94d, card.Background, ReportPalette.Border);
                _gfx.DrawString(SafeText(card.Label), _kpiLabelFont, ReportPalette.Muted, new XRect(x + 14d, startY + 14d, width - 28d, 15d), XStringFormats.TopLeft);
                DrawSingleLineFitted(card.Value, ReportPalette.Navy, new XRect(x + 14d, startY + 38d, width - 28d, 24d), 11d);
                DrawText(card.Caption, _smallFont, ReportPalette.Muted, new XRect(x + 14d, startY + 74d, width - 28d, 16d));
            }

            _y += 120d;
        }

        public void DrawInfoGrid(IReadOnlyList<InfoCell>? cells)
        {
            DrawSectionTitle("Сводка расчета");
            cells ??= Array.Empty<InfoCell>();
            double gap = 10d;
            double colWidth = (PageWidth - Margin * 2d - gap) / 2d;

            for (int rowStart = 0; rowStart < cells.Count; rowStart += 2)
            {
                InfoCell left = cells[rowStart];
                InfoCell? right = rowStart + 1 < cells.Count ? cells[rowStart + 1] : null;
                double rowHeight = Math.Max(EstimateInfoCellHeight(left, colWidth), EstimateInfoCellHeight(right, colWidth));

                EnsureSpace(rowHeight + 8d);
                DrawInfoCell(left, Margin, _y, colWidth, rowHeight);
                if (right is not null)
                {
                    DrawInfoCell(right, Margin + colWidth + gap, _y, colWidth, rowHeight);
                }

                _y += rowHeight + 8d;
            }

            _y += 8d;
        }

        private void DrawInfoCell(InfoCell cell, double x, double y, double width, double height)
        {
            DrawRoundedRect(x, y, width, height, XBrushes.White, ReportPalette.Border);
            _gfx.DrawString(SafeText(cell.Label), _smallFont, ReportPalette.Muted, new XRect(x + 11d, y + 8d, width - 22d, 12d), XStringFormats.TopLeft);
            DrawText(cell.Value, _bodyBoldFont, ReportPalette.Navy, new XRect(x + 11d, y + 21d, width - 22d, height - 26d));
        }

        private static double EstimateInfoCellHeight(InfoCell? cell, double width)
        {
            if (cell is null)
            {
                return 40d;
            }

            double valueHeight = EstimateTextHeight(cell.Value, width - 22d, new XFont(FontFamily, 9.5, XFontStyleEx.Bold));
            return Math.Max(42d, 27d + valueHeight);
        }

        public void DrawCallout(string title, string text, XBrush background)
        {
            double textWidth = PageWidth - Margin * 2d - 28d;
            double textHeight = EstimateTextHeight(text, textWidth, _bodyFont);
            double height = Math.Max(62d, 39d + textHeight);
            EnsureSpace(height + 18d);
            DrawRoundedRect(Margin, _y, PageWidth - Margin * 2d, height, background, ReportPalette.Border);
            _gfx.DrawString(SafeText(title), _bodyBoldFont, ReportPalette.Navy, new XRect(Margin + 14d, _y + 11d, textWidth, 14d), XStringFormats.TopLeft);
            DrawText(text, _bodyFont, ReportPalette.Text, new XRect(Margin + 14d, _y + 29d, textWidth, height - 35d));
            _y += height + 18d;
        }

        public void DrawTaxRows(string title, IReadOnlyList<TaxReportLine>? rows, string currency)
        {
            DrawSectionTitle(title);
            rows ??= Array.Empty<TaxReportLine>();
            if (rows.Count == 0)
            {
                DrawCallout("Нет данных", "Для выбранного периода нет строк детализации.", ReportPalette.GrayTint);
                return;
            }

            EnsureSpace(38d);
            double tableWidth = PageWidth - Margin * 2d;
            double labelWidth = 142d;
            double amountWidth = 105d;
            double noteWidth = tableWidth - labelWidth - amountWidth;

            DrawRoundedRect(Margin, _y, tableWidth, 28d, ReportPalette.Navy, ReportPalette.NavyPen);
            _gfx.DrawString("Показатель", _bodyBoldFont, XBrushes.White, new XRect(Margin + 12d, _y + 8d, labelWidth - 20d, 12d), XStringFormats.TopLeft);
            _gfx.DrawString("Сумма", _bodyBoldFont, XBrushes.White, new XRect(Margin + labelWidth, _y + 8d, amountWidth - 10d, 12d), XStringFormats.TopRight);
            _gfx.DrawString("Комментарий", _bodyBoldFont, XBrushes.White, new XRect(Margin + labelWidth + amountWidth + 12d, _y + 8d, noteWidth - 20d, 12d), XStringFormats.TopLeft);
            _y += 28d;

            foreach (TaxReportLine? row in rows)
            {
                if (row is null)
                {
                    continue;
                }

                string kind = SafeText(row.Kind, "Info");
                string note = SafeText(row.Note, string.Empty);
                string label = SafeText(row.Label);
                double labelHeight = EstimateTextHeight(label, labelWidth - 20d, _bodyBoldFont);
                double noteHeight = EstimateTextHeight(note, noteWidth - 20d, _smallFont);
                double rowHeight = Math.Max(42d, Math.Max(labelHeight, noteHeight) + 22d);
                EnsureSpace(rowHeight);
                XBrush amountBrush = row.Amount < 0m ? ReportPalette.Red : kind.Equals("Benefit", StringComparison.OrdinalIgnoreCase) ? ReportPalette.Green : ReportPalette.Navy;
                DrawRoundedRect(Margin, _y, tableWidth, rowHeight - 1d, XBrushes.White, ReportPalette.Border);
                DrawText(label, _bodyBoldFont, ReportPalette.Text, new XRect(Margin + 12d, _y + 12d, labelWidth - 20d, rowHeight - 18d));
                DrawTextRight(kind.Equals("Info", StringComparison.OrdinalIgnoreCase) ? "-" : FormatMoney(row.Amount, currency), _bodyBoldFont, amountBrush, new XRect(Margin + labelWidth, _y + 12d, amountWidth - 10d, rowHeight - 18d));
                DrawText(note, _smallFont, ReportPalette.Muted, new XRect(Margin + labelWidth + amountWidth + 12d, _y + 10d, noteWidth - 20d, rowHeight - 18d));
                _y += rowHeight;
            }

            _y += 18d;
        }

        public void DrawSimpleRows(IReadOnlyList<SimpleRow>? rows)
        {
            rows ??= Array.Empty<SimpleRow>();
            if (rows.Count == 0)
            {
                DrawCallout("Нет данных", "Раздел пуст для выбранного периода.", ReportPalette.GrayTint);
                return;
            }

            foreach (SimpleRow? row in rows)
            {
                if (row is null)
                {
                    continue;
                }

                EnsureSpace(38d);
                DrawRoundedRect(Margin, _y, PageWidth - Margin * 2d, 34d, XBrushes.White, ReportPalette.Border);
                _gfx.DrawString(SafeText(row.Label), _bodyBoldFont, ReportPalette.Text, new XRect(Margin + 12d, _y + 10d, 220d, 14d), XStringFormats.TopLeft);
                DrawTextRight(row.Value, _bodyBoldFont, ReportPalette.Navy, new XRect(Margin + 240d, _y + 8d, 120d, 20d));
                DrawText(row.Note, _smallFont, ReportPalette.Muted, new XRect(Margin + 375d, _y + 9d, 120d, 16d));
                _y += 40d;
            }

            _y += 8d;
        }

        public void DrawHorizontalBars(IReadOnlyList<(string Label, decimal Value)>? rows, string currency)
        {
            rows ??= Array.Empty<(string Label, decimal Value)>();
            IReadOnlyList<(string Label, decimal Value)> visibleRows = rows
                .Where(static row => row.Value > 0m)
                .OrderByDescending(static row => row.Value)
                .Take(8)
                .ToArray();

            if (visibleRows.Count == 0)
            {
                DrawCallout("График недоступен", "Для построения графика распределения пока нет положительных значений.", ReportPalette.GrayTint);
                return;
            }

            EnsureSpace(46d + visibleRows.Count * 34d);
            decimal max = visibleRows.Max(static row => row.Value);
            double labelWidth = 142d;
            double barWidth = 245d;
            double amountWidth = 100d;

            DrawCallout("График", "Горизонтальные бары показывают относительный вес категорий и крупнейших активов в текущем портфеле.", ReportPalette.BlueTint);

            foreach ((string label, decimal value) in visibleRows)
            {
                EnsureSpace(34d);
                double y = _y;
                double fillWidth = max <= 0m ? 0d : Math.Max(8d, (double)(value / max) * barWidth);
                DrawText(label, _bodyBoldFont, ReportPalette.Text, new XRect(Margin, y + 2d, labelWidth - 10d, 18d));
                DrawRoundedRect(Margin + labelWidth, y + 5d, barWidth, 10d, ReportPalette.GrayTint, ReportPalette.Border);
                DrawRoundedRect(Margin + labelWidth, y + 5d, fillWidth, 10d, ReportPalette.Navy, ReportPalette.NavyPen);
                _gfx.DrawString(FormatMoney(value, currency), _bodyBoldFont, ReportPalette.Navy, new XRect(Margin + labelWidth + barWidth + 12d, y + 1d, amountWidth, 16d), XStringFormats.TopRight);
                _y += 30d;
            }

            _y += 10d;
        }

        public void DrawAssetTable(IReadOnlyList<PortfolioReportAsset>? assets, string currency)
        {
            DrawSectionTitle("Список активов");
            assets ??= Array.Empty<PortfolioReportAsset>();
            if (assets.Count == 0)
            {
                DrawCallout("Нет активов", "В текущем портфеле нет активов для вывода в отчет.", ReportPalette.GrayTint);
                return;
            }

            double tableWidth = PageWidth - Margin * 2d;
            double tickerWidth = 70d;
            double nameWidth = 128d;
            double typeWidth = 68d;
            double qtyWidth = 72d;
            double priceWidth = 82d;
            double valueWidth = tableWidth - tickerWidth - nameWidth - typeWidth - qtyWidth - priceWidth;

            EnsureSpace(32d);
            DrawRoundedRect(Margin, _y, tableWidth, 27d, ReportPalette.Navy, ReportPalette.NavyPen);
            _gfx.DrawString("Тикер", _bodyBoldFont, XBrushes.White, new XRect(Margin + 10d, _y + 8d, tickerWidth - 14d, 12d), XStringFormats.TopLeft);
            _gfx.DrawString("Актив", _bodyBoldFont, XBrushes.White, new XRect(Margin + tickerWidth, _y + 8d, nameWidth - 8d, 12d), XStringFormats.TopLeft);
            _gfx.DrawString("Тип", _bodyBoldFont, XBrushes.White, new XRect(Margin + tickerWidth + nameWidth, _y + 8d, typeWidth - 8d, 12d), XStringFormats.TopLeft);
            _gfx.DrawString("Кол-во", _bodyBoldFont, XBrushes.White, new XRect(Margin + tickerWidth + nameWidth + typeWidth, _y + 8d, qtyWidth - 8d, 12d), XStringFormats.TopRight);
            _gfx.DrawString("Цена", _bodyBoldFont, XBrushes.White, new XRect(Margin + tickerWidth + nameWidth + typeWidth + qtyWidth, _y + 8d, priceWidth - 8d, 12d), XStringFormats.TopRight);
            _gfx.DrawString("Стоимость", _bodyBoldFont, XBrushes.White, new XRect(Margin + tickerWidth + nameWidth + typeWidth + qtyWidth + priceWidth, _y + 8d, valueWidth - 10d, 12d), XStringFormats.TopRight);
            _y += 27d;

            foreach (PortfolioReportAsset asset in assets)
            {
                EnsureSpace(42d);
                double y = _y;
                DrawRoundedRect(Margin, y, tableWidth, 38d, XBrushes.White, ReportPalette.Border);
                DrawText(asset.Ticker, _bodyBoldFont, ReportPalette.Text, new XRect(Margin + 10d, y + 8d, tickerWidth - 14d, 13d));
                DrawText(asset.Name, _smallFont, ReportPalette.Muted, new XRect(Margin + tickerWidth, y + 7d, nameWidth - 8d, 24d));
                DrawText(asset.Type, _smallFont, ReportPalette.Text, new XRect(Margin + tickerWidth + nameWidth, y + 8d, typeWidth - 8d, 13d));
                _gfx.DrawString(SafeText(asset.Quantity), _smallFont, ReportPalette.Text, new XRect(Margin + tickerWidth + nameWidth + typeWidth, y + 8d, qtyWidth - 8d, 13d), XStringFormats.TopRight);
                _gfx.DrawString(SafeText(asset.CurrentPrice), _smallFont, ReportPalette.Text, new XRect(Margin + tickerWidth + nameWidth + typeWidth + qtyWidth, y + 8d, priceWidth - 8d, 13d), XStringFormats.TopRight);
                _gfx.DrawString(SafeText(asset.TotalValue), _bodyBoldFont, ReportPalette.Navy, new XRect(Margin + tickerWidth + nameWidth + typeWidth + qtyWidth + priceWidth, y + 8d, valueWidth - 10d, 13d), XStringFormats.TopRight);
                _gfx.DrawString($"Доля {SafeText(asset.Share)} · 24ч {SafeText(asset.Change24H)}", _smallFont, ReportPalette.Muted, new XRect(Margin + tickerWidth + nameWidth + typeWidth, y + 22d, tableWidth - tickerWidth - nameWidth - typeWidth - 10d, 12d), XStringFormats.TopRight);
                _y += 42d;
            }

            _y += 10d;
        }

        public void DrawRateNote(string note)
        {
            DrawCallout("Курсы валют", note, ReportPalette.BlueTint);
        }

        public void DrawDisclaimer(string disclaimer)
        {
            EnsureSpace(78d);
            _gfx.DrawLine(ReportPalette.BorderPen, Margin, _y, PageWidth - Margin, _y);
            _y += 12d;
            _gfx.DrawString("Юридическая оговорка", _bodyBoldFont, ReportPalette.Muted, new XRect(Margin, _y, PageWidth - Margin * 2d, 13d), XStringFormats.TopLeft);
            _y += 17d;
            DrawText(disclaimer, _smallFont, ReportPalette.Muted, new XRect(Margin, _y, PageWidth - Margin * 2d, 38d));
            _y += 54d;
        }

        public void DrawSectionTitle(string title)
        {
            EnsureSpace(34d);
            _gfx.DrawString(SafeText(title), _sectionFont, ReportPalette.Navy, new XRect(Margin, _y, PageWidth - Margin * 2d, 19d), XStringFormats.TopLeft);
            _y += 27d;
        }

        private void DrawPageHeader()
        {
            _gfx.DrawString("Proxima", _brandFont, ReportPalette.Navy, new XRect(Margin, 24d, 160d, 24d), XStringFormats.TopLeft);
            _gfx.DrawString(_reportName, _smallFont, ReportPalette.Muted, new XRect(PageWidth - Margin - 210d, 28d, 210d, 12d), XStringFormats.TopRight);
            _gfx.DrawLine(ReportPalette.BorderPen, Margin, 58d, PageWidth - Margin, 58d);
            _y = 76d;
        }

        private void DrawFooter()
        {
            _gfx.DrawLine(ReportPalette.BorderPen, Margin, FooterY - 8d, PageWidth - Margin, FooterY - 8d);
            _gfx.DrawString($"Сформировано Proxima - {DateTime.Now:dd.MM.yyyy HH:mm}", _footerFont, ReportPalette.Muted, new XRect(Margin, FooterY, 260d, 12d), XStringFormats.TopLeft);
            _gfx.DrawString($"{_contextName} / стр. {_pageNumber}", _footerFont, ReportPalette.Muted, new XRect(PageWidth - Margin - 240d, FooterY, 240d, 12d), XStringFormats.TopRight);
        }

        private void EnsureSpace(double requiredHeight)
        {
            if (_y + requiredHeight <= FooterY - 18d)
            {
                return;
            }

            DrawFooter();
            _gfx.Dispose();
            _page = CreatePage();
            _gfx = XGraphics.FromPdfPage(_page);
            _pageNumber++;
            DrawPageHeader();
        }

        private PdfPage CreatePage()
        {
            PdfPage page = _document.AddPage();
            page.Width = XUnit.FromPoint(PageWidth);
            page.Height = XUnit.FromPoint(PageHeight);
            return page;
        }

        private void DrawSingleLineFitted(string? text, XBrush brush, XRect rect, double minimumFontSize)
        {
            string value = SafeText(text, string.Empty);
            if (string.IsNullOrWhiteSpace(value) || rect.Width <= 0d || rect.Height <= 0d)
            {
                return;
            }

            double size = _kpiValueFont.Size;
            XFont font = _kpiValueFont;
            while (size > minimumFontSize && _gfx.MeasureString(value, font).Width > rect.Width)
            {
                size -= 0.5d;
                font = new XFont(FontFamily, size, XFontStyleEx.Bold);
            }

            string fitted = value;
            while (fitted.Length > 1 && _gfx.MeasureString(fitted, font).Width > rect.Width)
            {
                fitted = fitted[..^1];
            }

            _gfx.DrawString(fitted, font, brush, rect, XStringFormats.TopLeft);
        }

        private void DrawText(string? text, XFont font, XBrush brush, XRect rect)
        {
            string value = SafeText(text, string.Empty);
            if (string.IsNullOrEmpty(value) || rect.Width <= 0d || rect.Height <= 0d)
            {
                return;
            }

            double lineHeight = Math.Max(font.Size + 3d, 10d);
            double y = rect.Top;
            foreach (string paragraph in value.Split('\n'))
            {
                IReadOnlyList<string> lines = WrapText(paragraph, rect.Width, font);
                if (lines.Count == 0)
                {
                    lines = [string.Empty];
                }

                foreach (string line in lines)
                {
                    if (y + lineHeight > rect.Bottom)
                    {
                        return;
                    }

                    _gfx.DrawString(line, font, brush, new XRect(rect.Left, y, rect.Width, lineHeight), XStringFormats.TopLeft);
                    y += lineHeight;
                }
            }
        }

        private void DrawTextRight(string? text, XFont font, XBrush brush, XRect rect)
        {
            string value = SafeText(text, string.Empty);
            if (string.IsNullOrEmpty(value) || rect.Width <= 0d || rect.Height <= 0d)
            {
                return;
            }

            double lineHeight = Math.Max(font.Size + 3d, 10d);
            double y = rect.Top;
            foreach (string line in WrapText(value, rect.Width, font))
            {
                if (y + lineHeight > rect.Bottom)
                {
                    return;
                }

                _gfx.DrawString(line, font, brush, new XRect(rect.Left, y, rect.Width, lineHeight), XStringFormats.TopRight);
                y += lineHeight;
            }
        }

        private IReadOnlyList<string> WrapText(string? text, double width, XFont font)
        {
            string value = SafeText(text, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return [];
            }

            List<string> lines = [];
            string current = string.Empty;

            foreach (string word in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (_gfx.MeasureString(word, font).Width > width)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        lines.Add(current);
                        current = string.Empty;
                    }

                    string chunk = string.Empty;
                    foreach (char ch in word)
                    {
                        string candidate = chunk + ch;
                        if (candidate.Length > 0 && _gfx.MeasureString(candidate, font).Width <= width)
                        {
                            chunk = candidate;
                            continue;
                        }

                        if (!string.IsNullOrEmpty(chunk))
                        {
                            lines.Add(chunk);
                        }

                        chunk = ch.ToString();
                    }

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        current = chunk;
                    }

                    continue;
                }

                string candidateLine = string.IsNullOrWhiteSpace(current) ? word : $"{current} {word}";
                if (_gfx.MeasureString(candidateLine, font).Width <= width)
                {
                    current = candidateLine;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        lines.Add(current);
                    }

                    current = word;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                lines.Add(current);
            }

            return lines;
        }

        private void DrawRoundedRect(double x, double y, double width, double height, XBrush fill, XPen border)
        {
            _gfx.DrawRoundedRectangle(border, fill, x, y, width, height, 12d, 12d);
        }

        private static double EstimateTextHeight(string? text, double width, XFont font)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return font.GetHeight() + 2d;
            }

            double averageCharacterWidth = Math.Max(4.2d, font.Size * 0.47d);
            int charactersPerLine = Math.Max(14, Convert.ToInt32(width / averageCharacterWidth));
            int lineCount = 0;
            foreach (string paragraph in text.Split('\n'))
            {
                lineCount += Math.Max(1, Convert.ToInt32(Math.Ceiling(paragraph.Length / (double)charactersPerLine)));
            }

            return lineCount * (font.GetHeight() + 2d);
        }
    }


    private static class NativeTaxPdfWriter
    {
        public static void Export(TaxReportRequest request, string path)
        {
            NativeFont font = NativeFont.LoadDefault();
            TaxPdfCanvas canvas = new(font);

            canvas.DrawTitle("Налоговый отчет", $"Черновик расчета за {request.Year} год");
            canvas.DrawKpiCards([
                new NativeKpiCard("Всего налогов", FormatMoney(request.TotalTaxDue, request.Currency), "Оценка суммы к уплате", ReportRgb.BlueTint),
                new NativeKpiCard("Облагаемая база", FormatMoney(request.TaxableBase, request.Currency), "После учета расходов и убытков", ReportRgb.GrayTint),
                new NativeKpiCard("Льготы / зачет", FormatMoney(request.TaxSaved, request.Currency), "Иностранный налог и допустимые расходы", ReportRgb.GreenTint),
            ]);

            canvas.DrawSectionTitle("Сводка расчета");
            canvas.DrawInfoGrid([
                new InfoCell("Портфель", request.UserDisplayName),
                new InfoCell("Профиль", request.TaxProfile),
                new InfoCell("Ставка РБ", $"{request.BaseRatePercent:0.##}%"),
                new InfoCell("Дивиденды", $"{request.DividendRatePercent:0.##}%"),
                new InfoCell("Порог дохода", request.IncomeThreshold <= 0m ? "не применяется" : FormatMoney(request.IncomeThreshold, request.Currency)),
                new InfoCell("Операции", request.TransactionCount.ToString(CultureInfo.InvariantCulture)),
                new InfoCell("Версия расчета", request.CalculationVersion),
                new InfoCell("Курсы валют", request.ExchangeRateNotes),
            ]);

            canvas.DrawCallout("Налоговый профиль", request.TaxProfileDescription, ReportRgb.BlueTint);
            canvas.DrawTaxRows("Детализация расчета", request.CalculationBreakdown, request.Currency);
            canvas.DrawTaxRows("Разбор налога", request.TaxBreakdown, request.Currency);
            canvas.DrawCallout("Курсы валют", request.ExchangeRateNotes, ReportRgb.BlueTint);
            canvas.DrawDisclaimer(request.LegalDisclaimer);

            canvas.Save(path, request.UserDisplayName);
        }
    }

    private sealed class TaxPdfCanvas
    {
        private const double PageWidth = 595d;
        private const double PageHeight = 842d;
        private const double Margin = 42d;
        private const double FooterY = 803d;
        private readonly NativeFont _font;
        private readonly List<string> _pages = [];
        private readonly Dictionary<ushort, int> _usedGlyphs = [];
        private StringBuilder _content = new();
        private double _y;
        private int _pageNumber;

        public TaxPdfCanvas(NativeFont font)
        {
            _font = font;
            _pageNumber = 1;
            StartPage();
        }

        public void Save(string path, string contextName)
        {
            DrawFooter(contextName);
            _pages.Add(_content.ToString());
            NativePdfFile.Save(path, _pages, _font.FontBytes, _usedGlyphs);
        }

        public void DrawTitle(string title, string subtitle)
        {
            EnsureSpace(94d);
            DrawText("Proxima", 42d, 28d, 17d, ReportRgb.Navy, 170d);
            DrawText("Налоговый отчет", PageWidth - Margin - 180d, 31d, 8d, ReportRgb.Muted, 180d, TextAlign.Right);
            DrawLine(Margin, 58d, PageWidth - Margin, 58d, ReportRgb.Border);
            _y = 82d;
            DrawText(title, Margin, _y, 27d, ReportRgb.Navy, PageWidth - Margin * 2d);
            _y += 36d;
            DrawWrappedText(subtitle, Margin, _y, PageWidth - Margin * 2d, 9.5d, ReportRgb.Muted, 2);
            _y += 48d;
        }

        public void DrawKpiCards(IReadOnlyList<NativeKpiCard> cards)
        {
            EnsureSpace(112d);
            double gap = 14d;
            double width = (PageWidth - Margin * 2d - gap * 2d) / 3d;
            double startY = _y;
            for (int i = 0; i < cards.Count && i < 3; i++)
            {
                NativeKpiCard card = cards[i];
                double x = Margin + i * (width + gap);
                DrawRect(x, startY, width, 92d, card.Background, ReportRgb.Border);
                DrawText(card.Label, x + 14d, startY + 14d, 8.5d, ReportRgb.Muted, width - 28d);
                DrawText(card.Value, x + 14d, startY + 38d, 18d, ReportRgb.Navy, width - 28d);
                DrawWrappedText(card.Caption, x + 14d, startY + 66d, width - 28d, 7.5d, ReportRgb.Muted, 2);
            }

            _y += 118d;
        }

        public void DrawSectionTitle(string title)
        {
            EnsureSpace(34d);
            DrawText(title, Margin, _y, 15d, ReportRgb.Navy, PageWidth - Margin * 2d);
            _y += 27d;
        }

        public void DrawInfoGrid(IReadOnlyList<InfoCell> cells)
        {
            double gap = 10d;
            double colWidth = (PageWidth - Margin * 2d - gap) / 2d;

            for (int rowStart = 0; rowStart < cells.Count; rowStart += 2)
            {
                InfoCell left = cells[rowStart];
                InfoCell? right = rowStart + 1 < cells.Count ? cells[rowStart + 1] : null;
                double rowHeight = Math.Max(EstimateInfoCellHeight(left, colWidth), EstimateInfoCellHeight(right, colWidth));

                EnsureSpace(rowHeight + 8d);
                DrawInfoCell(left, Margin, _y, colWidth, rowHeight);
                if (right is not null)
                {
                    DrawInfoCell(right, Margin + colWidth + gap, _y, colWidth, rowHeight);
                }

                _y += rowHeight + 8d;
            }

            _y += 8d;
        }

        private void DrawInfoCell(InfoCell cell, double x, double y, double width, double height)
        {
            int valueLines = IsRateInfoCell(cell) ? 6 : 2;
            DrawRect(x, y, width, height, ReportRgb.White, ReportRgb.Border);
            DrawText(cell.Label, x + 11d, y + 8d, 7.5d, ReportRgb.Muted, width - 22d);
            DrawWrappedText(cell.Value, x + 11d, y + 21d, width - 22d, 8.8d, ReportRgb.Navy, valueLines);
        }

        private static double EstimateInfoCellHeight(InfoCell? cell, double width)
        {
            if (cell is null)
            {
                return 40d;
            }

            int valueLines = IsRateInfoCell(cell) ? 6 : 2;
            double valueHeight = EstimateTextHeight(cell.Value, width - 22d, 8.8d, 1, valueLines);
            return Math.Max(42d, 26d + valueHeight);
        }

        private static bool IsRateInfoCell(InfoCell cell)
        {
            return string.Equals(cell.Label, "Курсы валют", StringComparison.OrdinalIgnoreCase);
        }

        public void DrawCallout(string title, string text, ReportRgb background)
        {
            double textWidth = PageWidth - Margin * 2d - 28d;
            double textHeight = EstimateTextHeight(text, textWidth, 8.8d, 1);
            double height = Math.Max(62d, 39d + textHeight);

            EnsureSpace(height + 14d);
            DrawRect(Margin, _y, PageWidth - Margin * 2d, height, background, ReportRgb.Border);
            DrawText(title, Margin + 14d, _y + 11d, 9.5d, ReportRgb.Navy, textWidth);
            DrawWrappedText(text, Margin + 14d, _y + 29d, textWidth, 8.8d, ReportRgb.Text, int.MaxValue);
            _y += height + 18d;
        }

        public void DrawTaxRows(string title, IReadOnlyList<TaxReportLine>? rows, string currency)
        {
            DrawSectionTitle(title);
            rows ??= Array.Empty<TaxReportLine>();
            if (rows.Count == 0)
            {
                DrawCallout("Нет данных", "Для выбранного периода нет строк расчета.", ReportRgb.GrayTint);
                return;
            }

            double tableWidth = PageWidth - Margin * 2d;
            double labelWidth = 160d;
            double amountWidth = 110d;
            double noteWidth = tableWidth - labelWidth - amountWidth;
            EnsureSpace(34d);
            DrawRect(Margin, _y, tableWidth, 28d, ReportRgb.Navy, ReportRgb.Navy);
            DrawText("Показатель", Margin + 12d, _y + 8d, 8.5d, ReportRgb.White, labelWidth - 20d);
            DrawText("Сумма", Margin + labelWidth, _y + 8d, 8.5d, ReportRgb.White, amountWidth - 10d, TextAlign.Right);
            DrawText("Комментарий", Margin + labelWidth + amountWidth + 12d, _y + 8d, 8.5d, ReportRgb.White, noteWidth - 20d);
            _y += 28d;

            foreach (TaxReportLine? row in rows)
            {
                if (row is null)
                {
                    continue;
                }

                string kind = SafeText(row.Kind, "Info");
                string note = SafeText(row.Note, string.Empty);
                string label = SafeText(row.Label);
                double labelHeight = EstimateTextHeight(label, labelWidth - 20d, 8.8d, 1);
                double noteHeight = EstimateTextHeight(note, noteWidth - 20d, 7.5d, 1);
                double rowHeight = Math.Max(42d, Math.Max(labelHeight, noteHeight) + 22d);
                EnsureSpace(rowHeight);
                ReportRgb amountColor = row.Amount < 0m ? ReportRgb.Danger : kind.Equals("Benefit", StringComparison.OrdinalIgnoreCase) ? ReportRgb.Success : ReportRgb.Navy;
                DrawRect(Margin, _y, tableWidth, rowHeight - 1d, ReportRgb.White, ReportRgb.Border);
                DrawWrappedText(label, Margin + 12d, _y + 12d, labelWidth - 20d, 8.8d, ReportRgb.Text, int.MaxValue);
                DrawText(kind.Equals("Info", StringComparison.OrdinalIgnoreCase) ? "-" : FormatMoney(row.Amount, currency), Margin + labelWidth, _y + 12d, 8.5d, amountColor, amountWidth - 10d, TextAlign.Right);
                DrawWrappedText(note, Margin + labelWidth + amountWidth + 12d, _y + 10d, noteWidth - 20d, 7.5d, ReportRgb.Muted, int.MaxValue);
                _y += rowHeight;
            }

            _y += 18d;
        }

        public void DrawDisclaimer(string disclaimer)
        {
            double textWidth = PageWidth - Margin * 2d;
            double textHeight = EstimateTextHeight(disclaimer, textWidth, 7.2d, 1);
            EnsureSpace(34d + textHeight);
            DrawLine(Margin, _y, PageWidth - Margin, _y, ReportRgb.Border);
            _y += 12d;
            DrawText("Юридическая оговорка", Margin, _y, 8.8d, ReportRgb.Muted, textWidth);
            _y += 17d;
            DrawWrappedText(disclaimer, Margin, _y, textWidth, 7.2d, ReportRgb.Muted, int.MaxValue);
            _y += textHeight + 16d;
        }

        private void StartPage()
        {
            _content = new StringBuilder();
            _content.AppendLine("q");
            _content.AppendLine("1 1 1 rg 0 0 595 842 re f");
            _content.AppendLine("Q");
            _y = 76d;
        }

        private void EnsureSpace(double requiredHeight)
        {
            if (_y + requiredHeight <= FooterY - 18d)
            {
                return;
            }

            DrawFooter("Proxima");
            _pages.Add(_content.ToString());
            _pageNumber++;
            StartPage();
            DrawText("Proxima", 42d, 28d, 17d, ReportRgb.Navy, 170d);
            DrawText("Налоговый отчет", PageWidth - Margin - 180d, 31d, 8d, ReportRgb.Muted, 180d, TextAlign.Right);
            DrawLine(Margin, 58d, PageWidth - Margin, 58d, ReportRgb.Border);
            _y = 82d;
        }

        private void DrawFooter(string contextName)
        {
            DrawLine(Margin, FooterY - 8d, PageWidth - Margin, FooterY - 8d, ReportRgb.Border);
            DrawText($"Сформировано Proxima - {DateTime.Now:dd.MM.yyyy HH:mm}", Margin, FooterY, 7d, ReportRgb.Muted, 260d);
            DrawText($"{contextName} / стр. {_pageNumber}", PageWidth - Margin - 240d, FooterY, 7d, ReportRgb.Muted, 240d, TextAlign.Right);
        }

        private void DrawRect(double x, double yTop, double width, double height, ReportRgb fill, ReportRgb stroke)
        {
            _content.AppendLine("q");
            _content.AppendLine($"{fill.FillCommand()} {stroke.StrokeCommand()} 0.8 w {Fmt(x)} {Fmt(PageHeight - yTop - height)} {Fmt(width)} {Fmt(height)} re B");
            _content.AppendLine("Q");
        }

        private void DrawLine(double x1, double y1Top, double x2, double y2Top, ReportRgb color)
        {
            _content.AppendLine("q");
            _content.AppendLine($"{color.StrokeCommand()} 0.8 w {Fmt(x1)} {Fmt(PageHeight - y1Top)} m {Fmt(x2)} {Fmt(PageHeight - y2Top)} l S");
            _content.AppendLine("Q");
        }

        private void DrawWrappedText(string? text, double x, double yTop, double width, double fontSize, ReportRgb color, int maxLines)
        {
            string value = SafeText(text, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            IReadOnlyList<string> lines = WrapText(value, width, fontSize);
            double lineHeight = Math.Max(fontSize + 3d, 9d);
            int count = Math.Min(maxLines, lines.Count);
            for (int i = 0; i < count; i++)
            {
                string line = lines[i];
                if (i == count - 1 && lines.Count > count)
                {
                    line = TrimToFit(line, width, fontSize);
                }

                DrawText(line, x, yTop + i * lineHeight, fontSize, color, width);
            }
        }

        private void DrawText(string? text, double x, double yTop, double fontSize, ReportRgb color, double width, TextAlign align = TextAlign.Left)
        {
            string value = SafeText(text, string.Empty);
            if (string.IsNullOrWhiteSpace(value) || width <= 0d)
            {
                return;
            }

            value = TrimToFit(value, width, fontSize);
            double textWidth = EstimateTextWidth(value, fontSize);
            double startX = align switch
            {
                TextAlign.Right => x + Math.Max(0d, width - textWidth),
                TextAlign.Center => x + Math.Max(0d, (width - textWidth) / 2d),
                _ => x,
            };

            double baselineY = PageHeight - yTop - fontSize;
            double cursor = startX;
            foreach (Rune rune in value.EnumerateRunes())
            {
                if (rune.Value == '\n' || rune.Value == '\r')
                {
                    continue;
                }

                ushort glyphId = _font.GetGlyphId(rune.Value);
                if (glyphId == 0)
                {
                    glyphId = _font.GetGlyphId('?');
                }

                if (glyphId == 0)
                {
                    cursor += EstimateRuneAdvance(rune, fontSize);
                    continue;
                }

                _usedGlyphs.TryAdd(glyphId, rune.Value);
                _content.Append("BT /F1 ");
                _content.Append(Fmt(fontSize));
                _content.Append(" Tf ");
                _content.Append(color.FillCommand());
                _content.Append(" 1 0 0 1 ");
                _content.Append(Fmt(cursor));
                _content.Append(' ');
                _content.Append(Fmt(baselineY));
                _content.Append(" Tm <");
                _content.Append(glyphId.ToString("X4", CultureInfo.InvariantCulture));
                _content.AppendLine("> Tj ET");
                cursor += EstimateRuneAdvance(rune, fontSize);
                if (cursor > x + width + fontSize)
                {
                    break;
                }
            }
        }

        private static IReadOnlyList<string> WrapText(string? text, double width, double fontSize)
        {
            string value = SafeText(text, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return [];
            }

            int maxChars = Math.Max(12, Convert.ToInt32(width / Math.Max(4d, fontSize * 0.48d)));
            List<string> result = [];
            foreach (string paragraph in value.Split('\n'))
            {
                string current = string.Empty;
                foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = string.IsNullOrWhiteSpace(current) ? word : $"{current} {word}";
                    if (candidate.Length <= maxChars)
                    {
                        current = candidate;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        result.Add(current);
                    }

                    if (word.Length > maxChars)
                    {
                        for (int i = 0; i < word.Length; i += maxChars)
                        {
                            result.Add(word.Substring(i, Math.Min(maxChars, word.Length - i)));
                        }

                        current = string.Empty;
                    }
                    else
                    {
                        current = word;
                    }
                }

                if (!string.IsNullOrWhiteSpace(current))
                {
                    result.Add(current);
                }
            }

            return result;
        }

        private static double EstimateTextHeight(string? text, double width, double fontSize, int minimumLines)
        {
            return EstimateTextHeight(text, width, fontSize, minimumLines, int.MaxValue);
        }

        private static double EstimateTextHeight(string? text, double width, double fontSize, int minimumLines, int maxLines)
        {
            int wrappedLines = WrapText(text, width, fontSize).Count;
            int lines = Math.Max(minimumLines, Math.Min(maxLines, wrappedLines));
            return lines * Math.Max(fontSize + 3d, 9d);
        }

        private static double EstimateTextWidth(string text, double fontSize)
        {
            double width = 0d;
            foreach (Rune rune in text.EnumerateRunes())
            {
                width += EstimateRuneAdvance(rune, fontSize);
            }

            return width;
        }

        private static double EstimateRuneAdvance(Rune rune, double fontSize)
        {
            int value = rune.Value;
            double emWidth;

            if (value == ' ')
            {
                emWidth = 0.38d;
            }
            else
            {
                string text = char.ConvertFromUtf32(value);
                UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                bool isCyrillic = value >= 0x0400 && value <= 0x052F;

                if (".,:;!|'`".Contains(text, StringComparison.Ordinal))
                {
                    emWidth = 0.32d;
                }
                else if ("ijlI1|".Contains(text, StringComparison.Ordinal))
                {
                    emWidth = 0.38d;
                }
                else if ("mwMWШЩЮЖФДЫ".Contains(text, StringComparison.Ordinal))
                {
                    emWidth = isCyrillic ? 0.86d : 0.78d;
                }
                else if (category == UnicodeCategory.DecimalDigitNumber)
                {
                    emWidth = 0.62d;
                }
                else
                {
                    emWidth = isCyrillic ? 0.68d : 0.60d;
                }
            }

            double positiveTracking = Math.Clamp(fontSize * 0.035d, 0.22d, 0.55d);
            return fontSize * emWidth + positiveTracking;
        }

        private static string Fmt(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    private static class NativePdfFile
    {
        public static void Save(string path, IReadOnlyList<string> pageStreams, byte[] fontBytes, IReadOnlyDictionary<ushort, int> usedGlyphs)
        {
            List<PdfObj> objects = [];
            const int catalogId = 1;
            const int pagesId = 2;
            const int fontId = 3;
            const int cidFontId = 4;
            const int fontDescriptorId = 5;
            const int fontFileId = 6;
            const int toUnicodeId = 7;
            int nextId = 8;
            List<int> pageIds = [];
            foreach (string stream in pageStreams)
            {
                int pageId = nextId++;
                int contentId = nextId++;
                pageIds.Add(pageId);
                byte[] contentBytes = Encoding.ASCII.GetBytes(stream);
                objects.Add(new PdfObj(contentId, BuildStream("", contentBytes)));
                string page = $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontId} 0 R >> >> /Contents {contentId} 0 R >>";
                objects.Add(new PdfObj(pageId, Encoding.ASCII.GetBytes(page)));
            }

            string kids = string.Join(" ", pageIds.Select(id => $"{id} 0 R"));
            objects.Add(new PdfObj(catalogId, Encoding.ASCII.GetBytes($"<< /Type /Catalog /Pages {pagesId} 0 R >>")));
            objects.Add(new PdfObj(pagesId, Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [{kids}] /Count {pageIds.Count} >>")));
            objects.Add(new PdfObj(fontId, Encoding.ASCII.GetBytes($"<< /Type /Font /Subtype /Type0 /BaseFont /ProximaReportSans /Encoding /Identity-H /DescendantFonts [{cidFontId} 0 R] /ToUnicode {toUnicodeId} 0 R >>")));
            objects.Add(new PdfObj(cidFontId, Encoding.ASCII.GetBytes($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /ProximaReportSans /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {fontDescriptorId} 0 R /CIDToGIDMap /Identity /DW 600 >>")));
            objects.Add(new PdfObj(fontDescriptorId, Encoding.ASCII.GetBytes($"<< /Type /FontDescriptor /FontName /ProximaReportSans /Flags 32 /FontBBox [-1024 -400 2048 1600] /ItalicAngle 0 /Ascent 928 /Descent -236 /CapHeight 700 /StemV 80 /FontFile2 {fontFileId} 0 R >>")));
            objects.Add(new PdfObj(fontFileId, BuildStream($"/Length1 {fontBytes.Length}", fontBytes)));
            objects.Add(new PdfObj(toUnicodeId, BuildStream("", Encoding.ASCII.GetBytes(BuildToUnicode(usedGlyphs)))));
            objects.Sort(static (left, right) => left.Id.CompareTo(right.Id));

            using FileStream output = File.Create(path);
            WriteAscii(output, "%PDF-1.7\n%\u00E2\u00E3\u00CF\u00D3\n");
            List<long> offsets = [0L];
            foreach (PdfObj obj in objects)
            {
                while (offsets.Count < obj.Id)
                {
                    offsets.Add(0L);
                }

                offsets.Add(output.Position);
                WriteAscii(output, $"{obj.Id} 0 obj\n");
                output.Write(obj.Body, 0, obj.Body.Length);
                WriteAscii(output, "\nendobj\n");
            }

            long xref = output.Position;
            int maxId = objects.Max(static obj => obj.Id);
            WriteAscii(output, $"xref\n0 {maxId + 1}\n");
            WriteAscii(output, "0000000000 65535 f \n");
            for (int id = 1; id <= maxId; id++)
            {
                long offset = id < offsets.Count ? offsets[id] : 0L;
                WriteAscii(output, $"{offset:0000000000} 00000 n \n");
            }

            WriteAscii(output, $"trailer\n<< /Size {maxId + 1} /Root {catalogId} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        }

        private static byte[] BuildStream(string extraDictionary, byte[] bytes)
        {
            using MemoryStream stream = new();
            string dict = string.IsNullOrWhiteSpace(extraDictionary)
                ? $"<< /Length {bytes.Length} >>\nstream\n"
                : $"<< /Length {bytes.Length} {extraDictionary} >>\nstream\n";
            WriteAscii(stream, dict);
            stream.Write(bytes, 0, bytes.Length);
            WriteAscii(stream, "\nendstream");
            return stream.ToArray();
        }

        private static string BuildToUnicode(IReadOnlyDictionary<ushort, int> glyphs)
        {
            StringBuilder builder = new();
            builder.AppendLine("/CIDInit /ProcSet findresource begin");
            builder.AppendLine("12 dict begin");
            builder.AppendLine("begincmap");
            builder.AppendLine("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def");
            builder.AppendLine("/CMapName /ProximaReportSans-ToUnicode def");
            builder.AppendLine("/CMapType 2 def");
            builder.AppendLine("1 begincodespacerange");
            builder.AppendLine("<0000> <FFFF>");
            builder.AppendLine("endcodespacerange");
            List<KeyValuePair<ushort, int>> pairs = glyphs.OrderBy(static pair => pair.Key).ToList();
            for (int index = 0; index < pairs.Count; index += 100)
            {
                List<KeyValuePair<ushort, int>> chunk = pairs.Skip(index).Take(100).ToList();
                builder.AppendLine($"{chunk.Count} beginbfchar");
                foreach (KeyValuePair<ushort, int> pair in chunk)
                {
                    builder.Append('<');
                    builder.Append(pair.Key.ToString("X4", CultureInfo.InvariantCulture));
                    builder.Append("> <");
                    builder.Append(ToUtf16Hex(pair.Value));
                    builder.AppendLine(">");
                }

                builder.AppendLine("endbfchar");
            }

            builder.AppendLine("endcmap");
            builder.AppendLine("CMapName currentdict /CMap defineresource pop");
            builder.AppendLine("end");
            builder.AppendLine("end");
            return builder.ToString();
        }

        private static string ToUtf16Hex(int codePoint)
        {
            string text = char.ConvertFromUtf32(codePoint);
            byte[] bytes = Encoding.BigEndianUnicode.GetBytes(text);
            StringBuilder hex = new(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private sealed record PdfObj(int Id, byte[] Body);
    }

    private sealed class NativeFont
    {
        private readonly IReadOnlyDictionary<int, ushort> _glyphMap;

        private NativeFont(byte[] fontBytes, IReadOnlyDictionary<int, ushort> glyphMap)
        {
            FontBytes = fontBytes;
            _glyphMap = glyphMap;
        }

        public byte[] FontBytes { get; }

        public static NativeFont LoadDefault()
        {
            List<string> inspectedPaths = new();

            foreach (string path in EnumerateUnicodeFontCandidates().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                inspectedPaths.Add(path);

                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    IReadOnlyDictionary<int, ushort> glyphMap = TrueTypeCmapParser.Parse(bytes);

                    if (!SupportsReportAlphabet(glyphMap))
                    {
                        continue;
                    }

                    return new NativeFont(bytes, glyphMap);
                }
                catch (Exception) when (!System.Diagnostics.Debugger.IsAttached)
                {
                    // Some system fonts are collections, variable fonts, or malformed for this minimal writer.
                    // We intentionally skip them and try the next candidate instead of failing export.
                }
            }

            string inspected = inspectedPaths.Count == 0
                ? "каталоги шрифтов не найдены"
                : string.Join(", ", inspectedPaths.Take(12));

            throw new InvalidOperationException(
                "Не найден системный Unicode-шрифт для PDF. Установите DejaVu Sans, Noto Sans, Liberation Sans, Arial или Segoe UI. " +
                $"Проверенные пути: {inspected}");
        }

        public ushort GetGlyphId(int codePoint)
        {
            return _glyphMap.TryGetValue(codePoint, out ushort glyphId) ? glyphId : (ushort)0;
        }

        private static IEnumerable<string> EnumerateUnicodeFontCandidates()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            string[] directCandidates =
            [
                Path.Combine(windows, "Fonts", "segoeui.ttf"),
                Path.Combine(windows, "Fonts", "arial.ttf"),
                Path.Combine(windows, "Fonts", "arialuni.ttf"),
                Path.Combine(fonts, "segoeui.ttf"),
                Path.Combine(fonts, "arial.ttf"),
                Path.Combine(fonts, "arialuni.ttf"),

                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/TTF/DejaVuSans.ttf",
                "/usr/local/share/fonts/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/google-noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/google-noto-sans/NotoSans-Regular.ttf",
                "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
                "/usr/share/fonts/liberation/LiberationSans-Regular.ttf",
                "/usr/share/fonts/truetype/freefont/FreeSans.ttf",

                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
                "/Library/Fonts/Arial.ttf",
                "/Library/Fonts/Arial Unicode.ttf",
            ];

            foreach (string candidate in directCandidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    yield return candidate;
                }
            }

            string[] roots =
            [
                fonts,
                Path.Combine(windows, "Fonts"),
                "/usr/share/fonts",
                "/usr/local/share/fonts",
                "/run/host/fonts",
                "/System/Library/Fonts",
                "/Library/Fonts",
            ];

            foreach (string root in roots.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (string candidate in EnumerateFontFilesSafe(root))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateFontFilesSafe(string root)
        {
            if (!Directory.Exists(root))
            {
                yield break;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.ttf", SearchOption.AllDirectories);
            }
            catch (Exception) when (!System.Diagnostics.Debugger.IsAttached)
            {
                yield break;
            }

            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (name.Contains("dejavusans", StringComparison.Ordinal) ||
                    name.Contains("notosans", StringComparison.Ordinal) ||
                    name.Contains("liberationsans", StringComparison.Ordinal) ||
                    name.Contains("arial", StringComparison.Ordinal) ||
                    name.Contains("segoeui", StringComparison.Ordinal) ||
                    name.Contains("freesans", StringComparison.Ordinal) ||
                    name.Equals("ubuntu-r", StringComparison.Ordinal) ||
                    name.Equals("cantarell-regular", StringComparison.Ordinal))
                {
                    yield return file;
                }
            }
        }

        private static bool SupportsReportAlphabet(IReadOnlyDictionary<int, ushort> glyphMap)
        {
            const string probe = "Proxima Налоговый отчет Беларусь BYN USD 0123456789";
            foreach (char ch in probe)
            {
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                if (!glyphMap.TryGetValue(ch, out ushort glyphId) || glyphId == 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static class TrueTypeCmapParser
    {
        public static IReadOnlyDictionary<int, ushort> Parse(byte[] font)
        {
            int tableCount = ReadUInt16(font, 4);
            int cmapOffset = -1;
            for (int i = 0; i < tableCount; i++)
            {
                int offset = 12 + i * 16;
                string tag = Encoding.ASCII.GetString(font, offset, 4);
                if (tag == "cmap")
                {
                    cmapOffset = Convert.ToInt32(ReadUInt32(font, offset + 8));
                    break;
                }
            }

            if (cmapOffset < 0)
            {
                throw new InvalidOperationException("В выбранном шрифте не найдена таблица cmap.");
            }

            int subtableCount = ReadUInt16(font, cmapOffset + 2);
            int selectedOffset = -1;
            int selectedFormat = -1;
            int selectedPriority = int.MaxValue;
            for (int i = 0; i < subtableCount; i++)
            {
                int recordOffset = cmapOffset + 4 + i * 8;
                int platform = ReadUInt16(font, recordOffset);
                int encoding = ReadUInt16(font, recordOffset + 2);
                int subOffset = cmapOffset + Convert.ToInt32(ReadUInt32(font, recordOffset + 4));
                int format = ReadUInt16(font, subOffset);
                int priority = GetPriority(platform, encoding, format);
                if (priority < selectedPriority)
                {
                    selectedPriority = priority;
                    selectedOffset = subOffset;
                    selectedFormat = format;
                }
            }

            return selectedFormat switch
            {
                12 => ParseFormat12(font, selectedOffset),
                4 => ParseFormat4(font, selectedOffset),
                _ => throw new InvalidOperationException($"Неподдерживаемый формат cmap: {selectedFormat}."),
            };
        }

        private static int GetPriority(int platform, int encoding, int format)
        {
            if (format == 12 && platform == 3 && encoding == 10)
            {
                return 0;
            }

            if (format == 12 && platform == 0)
            {
                return 1;
            }

            if (format == 4 && platform == 3 && encoding == 1)
            {
                return 2;
            }

            if (format == 4 && platform == 0)
            {
                return 3;
            }

            return 100;
        }

        private static IReadOnlyDictionary<int, ushort> ParseFormat12(byte[] font, int offset)
        {
            int groupCount = Convert.ToInt32(ReadUInt32(font, offset + 12));
            Dictionary<int, ushort> map = [];
            for (int i = 0; i < groupCount; i++)
            {
                int groupOffset = offset + 16 + i * 12;
                uint start = ReadUInt32(font, groupOffset);
                uint end = ReadUInt32(font, groupOffset + 4);
                uint startGlyph = ReadUInt32(font, groupOffset + 8);
                for (uint cp = start; cp <= end && cp <= 0x10FFFF; cp++)
                {
                    uint glyph = startGlyph + cp - start;
                    if (glyph <= ushort.MaxValue)
                    {
                        map.TryAdd(Convert.ToInt32(cp), Convert.ToUInt16(glyph));
                    }
                }
            }

            return map;
        }

        private static IReadOnlyDictionary<int, ushort> ParseFormat4(byte[] font, int offset)
        {
            int segCount = ReadUInt16(font, offset + 6) / 2;
            int endCodeOffset = offset + 14;
            int startCodeOffset = endCodeOffset + segCount * 2 + 2;
            int idDeltaOffset = startCodeOffset + segCount * 2;
            int idRangeOffsetOffset = idDeltaOffset + segCount * 2;
            int glyphArrayOffset = idRangeOffsetOffset + segCount * 2;
            Dictionary<int, ushort> map = [];

            for (int segment = 0; segment < segCount; segment++)
            {
                int endCode = ReadUInt16(font, endCodeOffset + segment * 2);
                int startCode = ReadUInt16(font, startCodeOffset + segment * 2);
                int idDelta = ReadInt16(font, idDeltaOffset + segment * 2);
                int idRangeOffset = ReadUInt16(font, idRangeOffsetOffset + segment * 2);
                if (startCode == 0xFFFF && endCode == 0xFFFF)
                {
                    continue;
                }

                for (int cp = startCode; cp <= endCode; cp++)
                {
                    int glyphId;
                    if (idRangeOffset == 0)
                    {
                        glyphId = (cp + idDelta) & 0xFFFF;
                    }
                    else
                    {
                        int glyphIndexOffset = idRangeOffsetOffset + segment * 2 + idRangeOffset + (cp - startCode) * 2;
                        if (glyphIndexOffset < glyphArrayOffset || glyphIndexOffset + 1 >= font.Length)
                        {
                            continue;
                        }

                        glyphId = ReadUInt16(font, glyphIndexOffset);
                        if (glyphId != 0)
                        {
                            glyphId = (glyphId + idDelta) & 0xFFFF;
                        }
                    }

                    if (glyphId > 0)
                    {
                        map.TryAdd(cp, Convert.ToUInt16(glyphId));
                    }
                }
            }

            return map;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
        }

        private static short ReadInt16(byte[] bytes, int offset)
        {
            return unchecked((short)ReadUInt16(bytes, offset));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
        }
    }

    private readonly struct ReportRgb
    {
        public ReportRgb(double red, double green, double blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public double Red { get; }
        public double Green { get; }
        public double Blue { get; }

        public static readonly ReportRgb White = new(1d, 1d, 1d);
        public static readonly ReportRgb Navy = FromRgb(5, 28, 49);
        public static readonly ReportRgb Text = FromRgb(25, 38, 53);
        public static readonly ReportRgb Muted = FromRgb(102, 117, 139);
        public static readonly ReportRgb Success = FromRgb(18, 112, 66);
        public static readonly ReportRgb Danger = FromRgb(190, 38, 38);
        public static readonly ReportRgb Border = FromRgb(226, 232, 240);
        public static readonly ReportRgb BlueTint = FromRgb(235, 245, 255);
        public static readonly ReportRgb GreenTint = FromRgb(232, 250, 235);
        public static readonly ReportRgb GrayTint = FromRgb(247, 249, 252);

        public string FillCommand()
        {
            return $"{Value(Red)} {Value(Green)} {Value(Blue)} rg";
        }

        public string StrokeCommand()
        {
            return $"{Value(Red)} {Value(Green)} {Value(Blue)} RG";
        }

        private static ReportRgb FromRgb(byte red, byte green, byte blue)
        {
            return new ReportRgb(red / 255d, green / 255d, blue / 255d);
        }

        private static string Value(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    private sealed record NativeKpiCard(string Label, string Value, string Caption, ReportRgb Background);

    private enum TextAlign
    {
        Left,
        Center,
        Right,
    }

    private sealed class ProximaFontResolver : IFontResolver
    {
        public const string ReportFontFamily = "ProximaReportSans";
        private const string RegularFace = "ProximaReportSans-Regular";
        private const string BoldFace = "ProximaReportSans-Bold";
        private static readonly Lazy<FontFileSet> Fonts = new(LocateFonts);

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            return new FontResolverInfo(isBold ? BoldFace : RegularFace, false, isItalic);
        }

        public byte[] GetFont(string faceName)
        {
            FontFileSet fonts = Fonts.Value;
            string path = string.Equals(faceName, BoldFace, StringComparison.OrdinalIgnoreCase)
                ? fonts.BoldPath
                : fonts.RegularPath;

            return File.ReadAllBytes(path);
        }

        private static FontFileSet LocateFonts()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            string? regular = FirstExisting([
                Path.Combine(windows, "Fonts", "segoeui.ttf"),
                Path.Combine(windows, "Fonts", "arial.ttf"),
                Path.Combine(windows, "Fonts", "arialuni.ttf"),
                Path.Combine(fonts, "segoeui.ttf"),
                Path.Combine(fonts, "arial.ttf"),
                Path.Combine(fonts, "arialuni.ttf"),
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/TTF/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/google-noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/google-noto-sans/NotoSans-Regular.ttf",
                "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
                "/usr/share/fonts/liberation/LiberationSans-Regular.ttf",
                "/usr/share/fonts/truetype/freefont/FreeSans.ttf",
                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
                "/Library/Fonts/Arial.ttf",
                "/Library/Fonts/Arial Unicode.ttf",
            ]) ?? FirstExisting(EnumerateUnicodeFontCandidates());

            string? bold = FirstExisting([
                Path.Combine(windows, "Fonts", "segoeuib.ttf"),
                Path.Combine(windows, "Fonts", "arialbd.ttf"),
                Path.Combine(fonts, "segoeuib.ttf"),
                Path.Combine(fonts, "arialbd.ttf"),
                "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf",
                "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf",
                "/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf",
                "/usr/share/fonts/noto/NotoSans-Bold.ttf",
                "/usr/share/fonts/google-noto/NotoSans-Bold.ttf",
                "/usr/share/fonts/google-noto-sans/NotoSans-Bold.ttf",
                "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf",
                "/usr/share/fonts/liberation/LiberationSans-Bold.ttf",
                "/usr/share/fonts/truetype/freefont/FreeSansBold.ttf",
                "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
                "/Library/Fonts/Arial Bold.ttf",
            ]) ?? regular;

            if (regular is null)
            {
                throw new InvalidOperationException("Не найден системный Unicode-шрифт для PDF. Установите Arial, Segoe UI, DejaVu Sans, Liberation Sans, FreeSans или Noto Sans.");
            }

            return new FontFileSet(regular, bold ?? regular);
        }

        private static string? FirstExisting(IEnumerable<string> candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateUnicodeFontCandidates()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            string[] directCandidates =
            [
                Path.Combine(windows, "Fonts", "segoeui.ttf"),
                Path.Combine(windows, "Fonts", "arial.ttf"),
                Path.Combine(windows, "Fonts", "arialuni.ttf"),
                Path.Combine(fonts, "segoeui.ttf"),
                Path.Combine(fonts, "arial.ttf"),
                Path.Combine(fonts, "arialuni.ttf"),

                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/TTF/DejaVuSans.ttf",
                "/usr/local/share/fonts/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/google-noto/NotoSans-Regular.ttf",
                "/usr/share/fonts/google-noto-sans/NotoSans-Regular.ttf",
                "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
                "/usr/share/fonts/liberation/LiberationSans-Regular.ttf",
                "/usr/share/fonts/truetype/freefont/FreeSans.ttf",

                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
                "/Library/Fonts/Arial.ttf",
                "/Library/Fonts/Arial Unicode.ttf",
            ];

            foreach (string candidate in directCandidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    yield return candidate;
                }
            }

            string[] roots =
            [
                fonts,
                Path.Combine(windows, "Fonts"),
                "/usr/share/fonts",
                "/usr/local/share/fonts",
                "/run/host/fonts",
                "/System/Library/Fonts",
                "/Library/Fonts",
            ];

            foreach (string root in roots.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (string candidate in EnumerateFontFilesSafe(root))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateFontFilesSafe(string root)
        {
            if (!Directory.Exists(root))
            {
                yield break;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.ttf", SearchOption.AllDirectories);
            }
            catch (Exception) when (!System.Diagnostics.Debugger.IsAttached)
            {
                yield break;
            }

            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (name.Contains("dejavusans", StringComparison.Ordinal) ||
                    name.Contains("notosans", StringComparison.Ordinal) ||
                    name.Contains("liberationsans", StringComparison.Ordinal) ||
                    name.Contains("arial", StringComparison.Ordinal) ||
                    name.Contains("segoeui", StringComparison.Ordinal) ||
                    name.Contains("freesans", StringComparison.Ordinal) ||
                    name.Equals("ubuntu-r", StringComparison.Ordinal) ||
                    name.Equals("cantarell-regular", StringComparison.Ordinal))
                {
                    yield return file;
                }
            }
        }

        private static bool SupportsReportAlphabet(IReadOnlyDictionary<int, ushort> glyphMap)
        {
            const string probe = "Proxima Налоговый отчет Беларусь BYN USD 0123456789";
            foreach (char ch in probe)
            {
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                if (!glyphMap.TryGetValue(ch, out ushort glyphId) || glyphId == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed record FontFileSet(string RegularPath, string BoldPath);
    }

    private static class ReportPalette
    {
        public static readonly XBrush Navy = new XSolidBrush(XColor.FromArgb(5, 28, 49));
        public static readonly XBrush Text = new XSolidBrush(XColor.FromArgb(25, 38, 53));
        public static readonly XBrush Muted = new XSolidBrush(XColor.FromArgb(102, 117, 139));
        public static readonly XBrush Green = new XSolidBrush(XColor.FromArgb(18, 112, 66));
        public static readonly XBrush Red = new XSolidBrush(XColor.FromArgb(190, 38, 38));
        public static readonly XBrush BlueTint = new XSolidBrush(XColor.FromArgb(235, 245, 255));
        public static readonly XBrush GreenTint = new XSolidBrush(XColor.FromArgb(232, 250, 235));
        public static readonly XBrush RedTint = new XSolidBrush(XColor.FromArgb(255, 239, 239));
        public static readonly XBrush GrayTint = new XSolidBrush(XColor.FromArgb(247, 249, 252));
        public static readonly XPen Border = new(XColor.FromArgb(226, 232, 240), 0.8d);
        public static readonly XPen NavyPen = new(XColor.FromArgb(5, 28, 49), 0.8d);
        public static readonly XPen BorderPen = new(XColor.FromArgb(226, 232, 240), 0.8d);
    }

    private sealed record KpiCard(string Label, string Value, string Caption, XBrush Background);

    private sealed record InfoCell(string Label, string Value);

    private sealed record SimpleRow(string Label, string Value, string Note);
}
