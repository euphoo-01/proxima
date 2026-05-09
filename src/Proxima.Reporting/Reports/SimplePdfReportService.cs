using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Proxima.Reporting.Reports;

public sealed class SimplePdfReportService : IReportService
{
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly object FontResolverLock = new();

    public ReportPreviewResult PreviewPortfolio(PortfolioReportRequest request)
    {
        if (!ValidateOutputDirectory(request.OutputDirectory, out string message))
        {
            return new ReportPreviewResult(false, message, []);
        }

        List<string> sections =
        [
            "Portfolio Summary",
            "Allocation",
            "Top Assets",
            "Risk Metrics",
            "Transaction Summary",
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
            "Tax formula breakdown",
            "Exchange rate notes",
            "Legal disclaimer",
        ];

        return new ReportPreviewResult(true, "Preview ready.", sections);
    }

    public async Task<ReportExportResult> ExportPortfolioPdfAsync(PortfolioReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!ValidateOutputDirectory(request.OutputDirectory, out string message))
        {
            return new ReportExportResult(false, message, null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(request.OutputDirectory);
            string path = Path.Combine(request.OutputDirectory, BuildFileName("portfolio-report", request.PortfolioName));

            EnsureFontResolver();
            using PdfDocument document = new();
            document.Info.Title = "Proxima Portfolio Report";
            document.Info.Author = "Proxima";
            document.Info.Subject = request.PortfolioName;
            document.Info.CreationDate = DateTime.Now;

            ReportCanvas canvas = new(document, "Отчет по портфелю", request.PortfolioName);
            canvas.DrawTitle("Отчет по портфелю", request.PeriodLabel);
            canvas.DrawKpiCards([
                new KpiCard("Стоимость портфеля", FormatMoney(request.TotalValue, request.Currency), "Все активы в базовой валюте", ReportPalette.BlueTint),
                new KpiCard("P&L", FormatMoney(request.ProfitLoss, request.Currency), "Финансовый результат", request.ProfitLoss >= 0m ? ReportPalette.GreenTint : ReportPalette.RedTint),
                new KpiCard("Операции", request.TransactionCount.ToString(CultureInfo.InvariantCulture), "Количество транзакций", ReportPalette.GrayTint),
            ]);

            canvas.DrawSectionTitle("Распределение портфеля");
            canvas.DrawSimpleRows(request.Allocation.Select(item => new SimpleRow(item.Category, FormatMoney(item.Value, request.Currency), "Категория портфеля")).ToList());

            canvas.DrawSectionTitle("Крупнейшие активы");
            canvas.DrawSimpleRows(request.TopAssets.Select(item => new SimpleRow(item.Asset, FormatMoney(item.Value, request.Currency), "Текущая оценка")).ToList());

            canvas.DrawSectionTitle("Риск-метрики");
            canvas.DrawSimpleRows(request.RiskMetrics.Select(item => new SimpleRow(item.Metric, item.Value, string.Empty)).ToList());

            canvas.DrawDisclaimer(request.Disclaimer);
            canvas.Save(path);
            await Task.CompletedTask.ConfigureAwait(false);
            return new ReportExportResult(true, "Portfolio report exported.", path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ReportExportResult(false, $"Portfolio export failed: {ex.Message}", null);
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

            TaxReportRequest requestToRender = safeRequest;
            ReportCanvas canvas = new(document, "Налоговый отчет", requestToRender.UserDisplayName);
            canvas.DrawTitle("Налоговый отчет", $"Черновик расчета за {requestToRender.Year} год");
            canvas.DrawKpiCards([
                new KpiCard("Всего налогов", FormatMoney(requestToRender.TotalTaxDue, requestToRender.Currency), "Оценка суммы к уплате", ReportPalette.BlueTint),
                new KpiCard("Облагаемая база", FormatMoney(requestToRender.TaxableBase, requestToRender.Currency), "После учета расходов и убытков", ReportPalette.GrayTint),
                new KpiCard("Льготы / зачет", FormatMoney(requestToRender.TaxSaved, requestToRender.Currency), "Иностранный налог и допустимые расходы", ReportPalette.GreenTint),
            ]);

            canvas.DrawInfoGrid([
                new InfoCell("Портфель", requestToRender.UserDisplayName),
                new InfoCell("Профиль", requestToRender.TaxProfile),
                new InfoCell("Ставка РБ", $"{requestToRender.BaseRatePercent:0.##}%"),
                new InfoCell("Дивиденды", $"{requestToRender.DividendRatePercent:0.##}%"),
                new InfoCell("Порог дохода", requestToRender.IncomeThreshold <= 0m ? "не применяется" : FormatMoney(requestToRender.IncomeThreshold, requestToRender.Currency)),
                new InfoCell("Операции", request.TransactionCount.ToString(CultureInfo.InvariantCulture)),
                new InfoCell("Версия расчета", requestToRender.CalculationVersion),
                new InfoCell("Курсы валют", requestToRender.ExchangeRateNotes),
            ]);

            canvas.DrawCallout("Налоговый профиль", requestToRender.TaxProfileDescription, ReportPalette.BlueTint);
            canvas.DrawTaxFormula(requestToRender);
            canvas.DrawTaxRows("Детализация расчета", requestToRender.CalculationBreakdown, requestToRender.Currency);
            canvas.DrawTaxRows("Разбор налога", requestToRender.TaxBreakdown, requestToRender.Currency);
            canvas.DrawRateNote(requestToRender.ExchangeRateNotes);
            canvas.DrawDisclaimer(requestToRender.LegalDisclaimer);
            canvas.Save(path);
            await Task.CompletedTask.ConfigureAwait(false);
            return new ReportExportResult(true, "Tax report exported.", path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ReportExportResult(false, $"Tax export failed: {ex.Message}", null);
        }
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
            OutputDirectory = SafeText(request.OutputDirectory, ProximaReportingComposition.GetDefaultReportDirectory()),
        };
    }

    private static string SafeText(string? value, string fallback = "—")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
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
                _gfx.DrawString(SafeText(card.Value), _kpiValueFont, ReportPalette.Navy, new XRect(x + 14d, startY + 38d, width - 28d, 24d), XStringFormats.TopLeft);
                DrawText(card.Caption, _smallFont, ReportPalette.Muted, new XRect(x + 14d, startY + 66d, width - 28d, 18d));
            }

            _y += 120d;
        }

        public void DrawInfoGrid(IReadOnlyList<InfoCell>? cells)
        {
            DrawSectionTitle("Сводка расчета");
            cells ??= Array.Empty<InfoCell>();
            double gap = 10d;
            double colWidth = (PageWidth - Margin * 2d - gap) / 2d;
            double rowHeight = 40d;

            for (int i = 0; i < cells.Count; i++)
            {
                if (i % 2 == 0)
                {
                    EnsureSpace(rowHeight + 8d);
                }

                int col = i % 2;
                double x = Margin + col * (colWidth + gap);
                double y = _y;
                DrawRoundedRect(x, y, colWidth, rowHeight, XBrushes.White, ReportPalette.Border);
                InfoCell? cell = cells[i];
                _gfx.DrawString(SafeText(cell?.Label), _smallFont, ReportPalette.Muted, new XRect(x + 11d, y + 8d, colWidth - 22d, 12d), XStringFormats.TopLeft);
                DrawText(cell?.Value, _bodyBoldFont, ReportPalette.Navy, new XRect(x + 11d, y + 21d, colWidth - 22d, 16d));

                if (col == 1 || i == cells.Count - 1)
                {
                    _y += rowHeight + 8d;
                }
            }

            _y += 8d;
        }

        public void DrawCallout(string title, string text, XBrush background)
        {
            EnsureSpace(78d);
            double height = 62d;
            DrawRoundedRect(Margin, _y, PageWidth - Margin * 2d, height, background, ReportPalette.Border);
            _gfx.DrawString(SafeText(title), _bodyBoldFont, ReportPalette.Navy, new XRect(Margin + 14d, _y + 11d, PageWidth - Margin * 2d - 28d, 14d), XStringFormats.TopLeft);
            DrawText(text, _bodyFont, ReportPalette.Text, new XRect(Margin + 14d, _y + 29d, PageWidth - Margin * 2d - 28d, 24d));
            _y += height + 18d;
        }

        public void DrawTaxFormula(TaxReportRequest request)
        {
            DrawSectionTitle("Как получилась сумма к уплате");
            EnsureSpace(92d);
            double cardWidth = PageWidth - Margin * 2d;
            DrawRoundedRect(Margin, _y, cardWidth, 76d, ReportPalette.GrayTint, ReportPalette.Border);
            string formula = $"({FormatMoney(request.RealizedGains + request.Dividends, request.Currency)} - {FormatMoney(request.Fees + Math.Abs(request.Losses), request.Currency)}) -> база {FormatMoney(request.TaxableBase, request.Currency)}";
            string tax = $"Налог до зачета: {FormatMoney(request.TotalTaxDue + request.TaxSaved, request.Currency)}; зачет: {FormatMoney(request.TaxSaved, request.Currency)}; к уплате: {FormatMoney(request.TotalTaxDue, request.Currency)}.";
            DrawText(formula, _bodyBoldFont, ReportPalette.Navy, new XRect(Margin + 14d, _y + 14d, cardWidth - 28d, 18d));
            DrawText(tax, _bodyFont, ReportPalette.Text, new XRect(Margin + 14d, _y + 38d, cardWidth - 28d, 28d));
            _y += 94d;
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
                double rowHeight = Math.Max(42d, EstimateTextHeight(note, noteWidth - 24d, _smallFont) + 22d);
                EnsureSpace(rowHeight);
                XBrush amountBrush = row.Amount < 0m ? ReportPalette.Red : kind.Equals("Benefit", StringComparison.OrdinalIgnoreCase) ? ReportPalette.Green : ReportPalette.Navy;
                DrawRoundedRect(Margin, _y, tableWidth, rowHeight - 1d, XBrushes.White, ReportPalette.Border);
                DrawText(label, _bodyBoldFont, ReportPalette.Text, new XRect(Margin + 12d, _y + 12d, labelWidth - 20d, rowHeight - 18d));
                _gfx.DrawString(kind.Equals("Info", StringComparison.OrdinalIgnoreCase) ? "-" : FormatMoney(row.Amount, currency), _bodyBoldFont, amountBrush, new XRect(Margin + labelWidth, _y + 12d, amountWidth - 10d, 14d), XStringFormats.TopRight);
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
                _gfx.DrawString(SafeText(row.Value), _bodyBoldFont, ReportPalette.Navy, new XRect(Margin + 240d, _y + 10d, 120d, 14d), XStringFormats.TopRight);
                DrawText(row.Note, _smallFont, ReportPalette.Muted, new XRect(Margin + 375d, _y + 9d, 120d, 16d));
                _y += 40d;
            }

            _y += 8d;
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

        private void DrawText(string? text, XFont font, XBrush brush, XRect rect)
        {
            text = SafeText(text, string.Empty);
            XTextFormatter formatter = new(_gfx)
            {
                Alignment = XParagraphAlignment.Left,
            };
            formatter.DrawString(text, font, brush, rect, XStringFormats.TopLeft);
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
            string path = faceName.Equals(BoldFace, StringComparison.OrdinalIgnoreCase)
                ? fonts.BoldPath
                : fonts.RegularPath;

            return File.ReadAllBytes(path);
        }

        private static FontFileSet LocateFonts()
        {
            string? regular = FirstExisting([
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "segoeui.ttf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "arial.ttf"),
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
                "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
                "/System/Library/Fonts/Supplemental/Arial.ttf",
                "/Library/Fonts/Arial.ttf",
            ]);

            string? bold = FirstExisting([
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "segoeuib.ttf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "arialbd.ttf"),
                "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf",
                "/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc",
                "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
                "/Library/Fonts/Arial Bold.ttf",
            ]);

            if (regular is null || bold is null)
            {
                throw new InvalidOperationException("Не найден системный Unicode-шрифт для PDF. Установите Arial, Segoe UI, DejaVu Sans или Noto Sans.");
            }

            return new FontFileSet(regular, bold);
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
