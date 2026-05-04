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

    public static readonly StyledProperty<int> VisibleStartIndexProperty =
        AvaloniaProperty.Register<ProximaCandlestickChart, int>(nameof(VisibleStartIndex), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> VisibleEndIndexProperty =
        AvaloniaProperty.Register<ProximaCandlestickChart, int>(nameof(VisibleEndIndex), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> CurrencyCodeProperty =
        AvaloniaProperty.Register<ProximaCandlestickChart, string>(nameof(CurrencyCode), "USD");

    private int _hoverIndex = -1;
    private bool _isPanning;
    private Point _panStartPointer;
    private int _panStartVisibleStart;
    private int _panStartVisibleEnd;

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

    public int VisibleStartIndex
    {
        get => GetValue(VisibleStartIndexProperty);
        set => SetValue(VisibleStartIndexProperty, value);
    }

    public int VisibleEndIndex
    {
        get => GetValue(VisibleEndIndexProperty);
        set => SetValue(VisibleEndIndexProperty, value);
    }

    public string CurrencyCode
    {
        get => GetValue(CurrencyCodeProperty);
        set => SetValue(CurrencyCodeProperty, value);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        IReadOnlyList<CandlestickPointViewModel>? candles = Candles;
        if (candles is null || candles.Count < 3)
        {
            return;
        }

        (int start, int end) = NormalizeVisibleRange(candles.Count);
        int width = end - start + 1;
        int minWidth = 3;
        int nextWidth = e.Delta.Y > 0 ? Math.Max(minWidth, width - 1) : Math.Min(candles.Count, width + 1);
        if (nextWidth == width)
        {
            return;
        }

        int anchor = ToVisibleIndex(e.GetPosition(this).X, start, end);
        double ratio = width > 1 ? (anchor - start) / (double)(width - 1) : 0.5d;
        int newStart = anchor - (int)Math.Round((nextWidth - 1) * ratio);
        int newEnd = newStart + nextWidth - 1;
        ClampAndSetVisibleRange(candles.Count, newStart, newEnd);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        IReadOnlyList<CandlestickPointViewModel>? candles = Candles;
        if (candles is null || candles.Count == 0)
        {
            return;
        }

        (int start, int end) = NormalizeVisibleRange(candles.Count);
        _isPanning = true;
        _panStartPointer = e.GetPosition(this);
        _panStartVisibleStart = start;
        _panStartVisibleEnd = end;
        e.Pointer.Capture(this);
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

        (int visibleStart, int visibleEnd) = NormalizeVisibleRange(candles.Count);
        int visibleCount = visibleEnd - visibleStart + 1;
        const double leftPad = 56;
        const double rightPad = 14;
        double plotWidth = Math.Max(8, Bounds.Width - leftPad - rightPad);
        double x = e.GetPosition(this).X - leftPad;
        double slot = plotWidth / Math.Max(1, visibleCount);
        int localIndex = (int)Math.Floor(x / Math.Max(1, slot));
        _hoverIndex = Math.Clamp(visibleStart + localIndex, visibleStart, visibleEnd);

        if (_isPanning)
        {
            double deltaX = e.GetPosition(this).X - _panStartPointer.X;
            int shift = (int)Math.Round(deltaX / Math.Max(1, slot));
            int panStart = _panStartVisibleStart - shift;
            int panEnd = _panStartVisibleEnd - shift;
            ClampAndSetVisibleRange(candles.Count, panStart, panEnd);
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverIndex = -1;
        _isPanning = false;
        e.Pointer.Capture(null);
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

        (int visibleStart, int visibleEnd) = NormalizeVisibleRange(candles.Count);
        IReadOnlyList<CandlestickPointViewModel> visibleCandles = candles.Skip(visibleStart).Take(visibleEnd - visibleStart + 1).ToArray();
        decimal min = visibleCandles.Min(static c => c.Low);
        decimal max = visibleCandles.Max(static c => c.High);
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

        const double leftPad = 56;
        const double rightPad = 14;
        const double topPad = 18;
        const double bottomPad = 42;

        double width = Bounds.Width;
        double height = Bounds.Height;
        double plotWidth = Math.Max(8, width - leftPad - rightPad);
        double plotHeight = Math.Max(8, height - topPad - bottomPad);
        double originX = leftPad;
        double originY = topPad + plotHeight;

        DrawGridAndAxes(context, gridBrush, axisBrush, labelBrush, originX, topPad, plotWidth, plotHeight, min, range);

        double slot = plotWidth / visibleCandles.Count;
        double bodyWidth = Math.Max(3, slot * 0.55);

        for (int local = 0; local < visibleCandles.Count; local++)
        {
            CandlestickPointViewModel candle = visibleCandles[local];
            double x = originX + local * slot + (slot - bodyWidth) / 2d;
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

        if (_hoverIndex >= visibleStart && _hoverIndex <= visibleEnd)
        {
            CandlestickPointViewModel c = candles[_hoverIndex];
            string text = $"{ProximaChartTooltipFormatter.FormatDateTime(c.Timestamp)}";
            string text2 = $"O {FormatMoney(c.Open)}  H {FormatMoney(c.High)}  L {FormatMoney(c.Low)}";
            string text3 = $"C {FormatMoney(c.Close)}  V {ProximaChartTooltipFormatter.FormatValue(c.Volume, ProximaChartValueKind.Quantity)}";

            double boxWidth = Math.Min(260, Bounds.Width - 16);
            double boxHeight = 58;
            double boxX = 8;
            double boxY = Math.Max(6, topPad - 4);
            context.DrawRectangle(tooltipBackground, new Pen(borderBrush, 1), new Rect(boxX, boxY, boxWidth, boxHeight), 6, 6);
            DrawText(context, text, labelBrush, boxX + 8, boxY + 5, ProximaChartTheme.TooltipFontSize);
            DrawText(context, text2, labelBrush, boxX + 8, boxY + 22, ProximaChartTheme.TooltipFontSize);
            DrawText(context, text3, labelBrush, boxX + 8, boxY + 39, ProximaChartTheme.TooltipFontSize);
        }

        DrawXAxisLabels(context, labelBrush, originX, topPad, plotWidth, plotHeight, candles, visibleStart, visibleEnd);
        DrawText(context, "Период", labelBrush, originX + plotWidth - 54, originY + 15, ProximaChartTheme.AxisLabelFontSize);
    }

    private void DrawGridAndAxes(DrawingContext context, IBrush gridBrush, IBrush axisBrush, IBrush labelBrush, double originX, double topPad, double plotWidth, double plotHeight, decimal min, decimal range)
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
            DrawText(context, FormatMoney(value), labelBrush, 4, y - 8, ProximaChartTheme.AxisLabelFontSize);
        }

        context.DrawLine(axisPen, new Point(originX, topPad), new Point(originX, topPad + plotHeight));
        context.DrawLine(axisPen, new Point(originX, topPad + plotHeight), new Point(originX + plotWidth, topPad + plotHeight));
        DrawText(context, "Цена", labelBrush, 6, topPad - 14, ProximaChartTheme.AxisLabelFontSize);
    }

    private static void DrawStateText(DrawingContext context, string text)
    {
        IBrush brush = Brushes.Gray;
        if (Avalonia.Application.Current is not null
            && Avalonia.Application.Current.TryFindResource("ProximaBrush.TextMuted", Avalonia.Application.Current.ActualThemeVariant, out object? rawBrush)
            && rawBrush is IBrush resolved)
        {
            brush = resolved;
        }

        FormattedText formatted = new(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12,
            brush);
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

    private string FormatMoney(decimal value)
    {
        return $"{ProximaChartTooltipFormatter.FormatValue(value, ProximaChartValueKind.Money)} {CurrencyCode}";
    }

    private static void DrawXAxisLabels(
        DrawingContext context,
        IBrush labelBrush,
        double originX,
        double topPad,
        double plotWidth,
        double plotHeight,
        IReadOnlyList<CandlestickPointViewModel> candles,
        int visibleStart,
        int visibleEnd)
    {
        int visibleCount = visibleEnd - visibleStart + 1;
        int labelCount = Math.Min(4, visibleCount);
        for (int i = 0; i < labelCount; i++)
        {
            double t = labelCount == 1 ? 0d : i / (double)(labelCount - 1);
            int index = visibleStart + (int)Math.Round((visibleCount - 1) * t);
            index = Math.Clamp(index, visibleStart, visibleEnd);
            string label = ProximaChartAxisFactory.FormatDateLabel(candles[index].Timestamp, candles[visibleStart].Timestamp, candles[visibleEnd].Timestamp);
            double x = originX + plotWidth * t - 24;
            DrawText(context, label, labelBrush, x, topPad + plotHeight + 8, ProximaChartTheme.AxisLabelFontSize);
        }
    }

    private (int Start, int End) NormalizeVisibleRange(int count)
    {
        if (count <= 0)
        {
            return (0, 0);
        }

        int start = Math.Clamp(VisibleStartIndex, 0, count - 1);
        int end = Math.Clamp(VisibleEndIndex, 0, count - 1);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        if (end == start && count > 1)
        {
            end = Math.Min(count - 1, start + 1);
        }

        if (start != VisibleStartIndex)
        {
            VisibleStartIndex = start;
        }

        if (end != VisibleEndIndex)
        {
            VisibleEndIndex = end;
        }

        return (start, end);
    }

    private void ClampAndSetVisibleRange(int count, int start, int end)
    {
        int width = end - start + 1;
        if (width <= 0)
        {
            return;
        }

        if (start < 0)
        {
            start = 0;
            end = width - 1;
        }

        if (end >= count)
        {
            end = count - 1;
            start = Math.Max(0, end - width + 1);
        }

        VisibleStartIndex = Math.Clamp(start, 0, count - 1);
        VisibleEndIndex = Math.Clamp(end, 0, count - 1);
        InvalidateVisual();
    }

    private int ToVisibleIndex(double pointerX, int start, int end)
    {
        int visibleCount = end - start + 1;
        const double leftPad = 56;
        const double rightPad = 14;
        double plotWidth = Math.Max(8, Bounds.Width - leftPad - rightPad);
        double localX = pointerX - leftPad;
        double slot = plotWidth / Math.Max(1, visibleCount);
        int local = (int)Math.Floor(localX / Math.Max(1, slot));
        local = Math.Clamp(local, 0, Math.Max(0, visibleCount - 1));
        return start + local;
    }
}
