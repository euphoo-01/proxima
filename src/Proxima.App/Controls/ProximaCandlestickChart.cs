using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Proxima.App.DesignSystem.Charts;
using Proxima.App.ViewModels;

namespace Proxima.App.Controls;

public sealed class ProximaCandlestickChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<CandlestickPointViewModel>?> CandlesProperty =
        AvaloniaProperty.Register<ProximaCandlestickChart, IReadOnlyList<CandlestickPointViewModel>?>(nameof(Candles));

    public static readonly StyledProperty<string?> EmptyStateTextProperty =
        AvaloniaProperty.Register<ProximaCandlestickChart, string?>(nameof(EmptyStateText), "Нет свечных данных");

    public static readonly StyledProperty<string?> ErrorStateTextProperty =
        AvaloniaProperty.Register<ProximaCandlestickChart, string?>(nameof(ErrorStateText));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ProximaCandlestickChart, bool>(nameof(IsLoading));

    private int _hoverIndex = -1;

    public IReadOnlyList<CandlestickPointViewModel>? Candles
    {
        get => GetValue(CandlesProperty);
        set => SetValue(CandlesProperty, value);
    }

    public string? EmptyStateText
    {
        get => GetValue(EmptyStateTextProperty);
        set => SetValue(EmptyStateTextProperty, value);
    }

    public string? ErrorStateText
    {
        get => GetValue(ErrorStateTextProperty);
        set => SetValue(ErrorStateTextProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        IReadOnlyList<CandlestickPointViewModel>? candles = Candles;
        if (candles is null || candles.Count == 0)
        {
            _hoverIndex = -1;
            InvalidateVisual();
            return;
        }

        const double leftPad = 52;
        const double rightPad = 14;
        double plotWidth = Math.Max(8, Bounds.Width - leftPad - rightPad);
        double x = e.GetPosition(this).X - leftPad;
        double slot = plotWidth / candles.Count;
        int index = (int)Math.Floor(x / Math.Max(1, slot));
        _hoverIndex = Math.Clamp(index, 0, candles.Count - 1);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverIndex = -1;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (IsLoading)
        {
            DrawStateText(context, "Загрузка графика...");
            return;
        }

        if (!string.IsNullOrWhiteSpace(ErrorStateText))
        {
            DrawStateText(context, ErrorStateText!);
            return;
        }

        IReadOnlyList<CandlestickPointViewModel>? candles = Candles;
        if (candles is null || candles.Count == 0)
        {
            DrawStateText(context, EmptyStateText ?? "Нет данных");
            return;
        }

        decimal min = candles.Min(static c => c.Low);
        decimal max = candles.Max(static c => c.High);
        decimal range = max - min;
        if (range <= 0m)
        {
            range = 1m;
        }

        IBrush axisBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.TextMuted");
        IBrush labelBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.TextSecondary");
        IBrush gridBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.BorderSubtle");
        IBrush upBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Success");
        IBrush downBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Danger");
        IBrush borderBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Border");
        IBrush tooltipBackground = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Surface");

        const double leftPad = 52;
        const double rightPad = 14;
        const double topPad = 18;
        const double bottomPad = 36;

        double width = Bounds.Width;
        double height = Bounds.Height;
        double plotWidth = Math.Max(8, width - leftPad - rightPad);
        double plotHeight = Math.Max(8, height - topPad - bottomPad);
        double originX = leftPad;
        double originY = topPad + plotHeight;

        DrawGridAndAxes(context, gridBrush, axisBrush, labelBrush, originX, topPad, plotWidth, plotHeight, min, range);

        double slot = plotWidth / candles.Count;
        double bodyWidth = Math.Max(3, slot * 0.55);

        for (int i = 0; i < candles.Count; i++)
        {
            CandlestickPointViewModel candle = candles[i];
            double x = originX + i * slot + (slot - bodyWidth) / 2d;
            double center = x + bodyWidth / 2d;
            double yHigh = Map(candle.High, min, range, topPad, plotHeight);
            double yLow = Map(candle.Low, min, range, topPad, plotHeight);
            double yOpen = Map(candle.Open, min, range, topPad, plotHeight);
            double yClose = Map(candle.Close, min, range, topPad, plotHeight);

            bool isUp = candle.Close >= candle.Open;
            IBrush candleBrush = isUp ? upBrush : downBrush;
            Pen candlePen = new(candleBrush, 1);
            context.DrawLine(candlePen, new Point(center, yHigh), new Point(center, yLow));

            double top = Math.Min(yOpen, yClose);
            double bodyHeight = Math.Max(2, Math.Abs(yClose - yOpen));
            context.DrawRectangle(candleBrush, candlePen, new Rect(x, top, bodyWidth, bodyHeight));
        }

        if (_hoverIndex >= 0 && _hoverIndex < candles.Count)
        {
            CandlestickPointViewModel c = candles[_hoverIndex];
            string text = $"O {ProximaChartTooltipFormatter.FormatValue(c.Open, ProximaChartValueKind.Money)}  H {ProximaChartTooltipFormatter.FormatValue(c.High, ProximaChartValueKind.Money)}";
            string text2 = $"L {ProximaChartTooltipFormatter.FormatValue(c.Low, ProximaChartValueKind.Money)}  C {ProximaChartTooltipFormatter.FormatValue(c.Close, ProximaChartValueKind.Money)}";

            double boxWidth = Math.Min(260, Bounds.Width - 16);
            double boxHeight = 42;
            double boxX = 8;
            double boxY = Math.Max(6, topPad - 4);
            context.DrawRectangle(tooltipBackground, new Pen(borderBrush, 1), new Rect(boxX, boxY, boxWidth, boxHeight), 6, 6);
            DrawText(context, text, labelBrush, boxX + 8, boxY + 6, ProximaChartTheme.TooltipFontSize);
            DrawText(context, text2, labelBrush, boxX + 8, boxY + 22, ProximaChartTheme.TooltipFontSize);
        }

        DrawText(context, "Период", labelBrush, originX + plotWidth - 54, originY + 12, ProximaChartTheme.AxisLabelFontSize);
    }

    private static void DrawGridAndAxes(DrawingContext context, IBrush gridBrush, IBrush axisBrush, IBrush labelBrush, double originX, double topPad, double plotWidth, double plotHeight, decimal min, decimal range)
    {
        Pen gridPen = new(gridBrush, ProximaChartTheme.GridStrokeThickness);
        Pen axisPen = new(axisBrush, ProximaChartTheme.AxisStrokeThickness);

        int gridLines = 4;
        for (int i = 0; i <= gridLines; i++)
        {
            double t = i / (double)gridLines;
            double y = topPad + plotHeight * t;
            context.DrawLine(gridPen, new Point(originX, y), new Point(originX + plotWidth, y));

            decimal value = min + range * (decimal)(1d - t);
            DrawText(context, ProximaChartTooltipFormatter.FormatValue(value, ProximaChartValueKind.Money), labelBrush, 4, y - 8, ProximaChartTheme.AxisLabelFontSize);
        }

        context.DrawLine(axisPen, new Point(originX, topPad), new Point(originX, topPad + plotHeight));
        context.DrawLine(axisPen, new Point(originX, topPad + plotHeight), new Point(originX + plotWidth, topPad + plotHeight));
        DrawText(context, "Цена", labelBrush, 6, topPad - 14, ProximaChartTheme.AxisLabelFontSize);
    }

    private static void DrawStateText(DrawingContext context, string text)
    {
        FormattedText formatted = new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12,
            Brushes.Gray);
        context.DrawText(formatted, new Point(12, 12));
    }

    private static void DrawText(DrawingContext context, string text, IBrush brush, double x, double y, double size)
    {
        FormattedText formatted = new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            size,
            brush);
        context.DrawText(formatted, new Point(x, y));
    }

    private static double Map(decimal value, decimal min, decimal range, double topPad, double plotHeight)
    {
        double normalized = (double)((value - min) / range);
        return topPad + plotHeight * (1d - normalized);
    }
}
