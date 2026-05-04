using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Proxima.App.DesignSystem.Charts;

namespace Proxima.App.Controls;

public sealed class ProximaCartesianChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<decimal>?> ValuesProperty =
        AvaloniaProperty.Register<ProximaCartesianChart, IReadOnlyList<decimal>?>(nameof(Values));

    public static readonly StyledProperty<ProximaChartValueKind> ValueKindProperty =
        AvaloniaProperty.Register<ProximaCartesianChart, ProximaChartValueKind>(nameof(ValueKind), ProximaChartValueKind.Money);

    public static readonly StyledProperty<string> XAxisTitleProperty =
        AvaloniaProperty.Register<ProximaCartesianChart, string>(nameof(XAxisTitle), "Период");

    public static readonly StyledProperty<string> YAxisTitleProperty =
        AvaloniaProperty.Register<ProximaCartesianChart, string>(nameof(YAxisTitle), "Сумма");

    public static readonly StyledProperty<string?> EmptyStateTextProperty =
        AvaloniaProperty.Register<ProximaCartesianChart, string?>(nameof(EmptyStateText), "Нет данных для графика");

    public static readonly StyledProperty<string?> ErrorStateTextProperty =
        AvaloniaProperty.Register<ProximaCartesianChart, string?>(nameof(ErrorStateText));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ProximaCartesianChart, bool>(nameof(IsLoading));

    private int _hoverIndex = -1;

    public IReadOnlyList<decimal>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public ProximaChartValueKind ValueKind
    {
        get => GetValue(ValueKindProperty);
        set => SetValue(ValueKindProperty, value);
    }

    public string XAxisTitle
    {
        get => GetValue(XAxisTitleProperty);
        set => SetValue(XAxisTitleProperty, value);
    }

    public string YAxisTitle
    {
        get => GetValue(YAxisTitleProperty);
        set => SetValue(YAxisTitleProperty, value);
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
        IReadOnlyList<decimal>? values = Values;
        if (values is null || values.Count == 0)
        {
            _hoverIndex = -1;
            InvalidateVisual();
            return;
        }

        const double leftPad = 46;
        const double rightPad = 14;
        double plotWidth = Math.Max(8, Bounds.Width - leftPad - rightPad);
        double x = e.GetPosition(this).X - leftPad;
        double step = values.Count <= 1 ? plotWidth : plotWidth / (values.Count - 1);
        int index = (int)Math.Round(x / Math.Max(1, step), MidpointRounding.AwayFromZero);
        _hoverIndex = Math.Clamp(index, 0, values.Count - 1);
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

        IReadOnlyList<decimal>? values = Values;
        if (values is null || values.Count == 0)
        {
            DrawStateText(context, EmptyStateText ?? "Нет данных");
            return;
        }

        decimal min = values.Min();
        decimal max = values.Max();
        decimal range = max - min;
        if (range <= 0m)
        {
            range = 1m;
        }

        IBrush axisBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.TextMuted");
        IBrush labelBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.TextSecondary");
        IBrush gridBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.BorderSubtle");
        IBrush lineBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Brand");
        IBrush fillBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.BrandHover");
        IBrush tooltipBackground = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Surface");

        const double leftPad = 46;
        const double rightPad = 14;
        const double topPad = 18;
        const double bottomPad = 36;

        double width = Bounds.Width;
        double height = Bounds.Height;
        double plotWidth = Math.Max(8, width - leftPad - rightPad);
        double plotHeight = Math.Max(8, height - topPad - bottomPad);
        double originX = leftPad;
        double originY = topPad + plotHeight;
        double step = values.Count <= 1 ? plotWidth : plotWidth / (values.Count - 1);

        DrawGridAndAxes(context, gridBrush, axisBrush, labelBrush, originX, topPad, plotWidth, plotHeight, min, range);

        StreamGeometry lineGeometry = new();
        StreamGeometry areaGeometry = new();
        using (StreamGeometryContext line = lineGeometry.Open())
        using (StreamGeometryContext area = areaGeometry.Open())
        {
            Point first = ToPoint(0, values[0], min, range, step, originX, topPad, plotHeight);
            line.BeginFigure(first, false);
            area.BeginFigure(new Point(first.X, originY), true);
            area.LineTo(first);

            for (int i = 1; i < values.Count; i++)
            {
                Point point = ToPoint(i, values[i], min, range, step, originX, topPad, plotHeight);
                line.LineTo(point);
                area.LineTo(point);
            }

            Point last = ToPoint(values.Count - 1, values[^1], min, range, step, originX, topPad, plotHeight);
            area.LineTo(new Point(last.X, originY));
            area.EndFigure(true);
        }

        context.DrawGeometry(fillBrush, null, areaGeometry);
        context.DrawGeometry(null, new Pen(lineBrush, ProximaChartTheme.SeriesStrokeThickness), lineGeometry);

        if (_hoverIndex >= 0 && _hoverIndex < values.Count)
        {
            DrawHoverTooltip(context, values, min, range, step, originX, topPad, plotHeight, tooltipBackground, labelBrush);
        }
    }

    private void DrawGridAndAxes(
        DrawingContext context,
        IBrush gridBrush,
        IBrush axisBrush,
        IBrush labelBrush,
        double originX,
        double topPad,
        double plotWidth,
        double plotHeight,
        decimal min,
        decimal range)
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
            DrawText(context, ProximaChartTooltipFormatter.FormatValue(value, ValueKind), labelBrush, 4, y - 8, ProximaChartTheme.AxisLabelFontSize);
        }

        context.DrawLine(axisPen, new Point(originX, topPad), new Point(originX, topPad + plotHeight));
        context.DrawLine(axisPen, new Point(originX, topPad + plotHeight), new Point(originX + plotWidth, topPad + plotHeight));

        DrawText(context, YAxisTitle, labelBrush, 6, topPad - 14, ProximaChartTheme.AxisLabelFontSize);
        DrawText(context, XAxisTitle, labelBrush, originX + plotWidth - 66, topPad + plotHeight + 12, ProximaChartTheme.AxisLabelFontSize);
    }

    private void DrawHoverTooltip(
        DrawingContext context,
        IReadOnlyList<decimal> values,
        decimal min,
        decimal range,
        double step,
        double originX,
        double topPad,
        double plotHeight,
        IBrush tooltipBackground,
        IBrush labelBrush)
    {
        Point p = ToPoint(_hoverIndex, values[_hoverIndex], min, range, step, originX, topPad, plotHeight);
        IBrush markerBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Brand");
        IBrush borderBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Border");

        context.DrawEllipse(markerBrush, new Pen(borderBrush, 1), p, 4, 4);

        string text = ProximaChartTooltipFormatter.FormatValue(values[_hoverIndex], ValueKind);
        double boxWidth = 104;
        double boxHeight = 26;
        double boxX = Math.Clamp(p.X - boxWidth / 2d, 8, Bounds.Width - boxWidth - 8);
        double boxY = Math.Max(6, p.Y - boxHeight - 8);

        context.DrawRectangle(tooltipBackground, new Pen(borderBrush, 1), new Rect(boxX, boxY, boxWidth, boxHeight), 6, 6);
        DrawText(context, text, labelBrush, boxX + 8, boxY + 6, ProximaChartTheme.TooltipFontSize);
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

    private static Point ToPoint(int index, decimal value, decimal min, decimal range, double step, double originX, double topPad, double plotHeight)
    {
        double x = originX + index * step;
        double normalized = (double)((value - min) / range);
        double y = topPad + plotHeight * (1d - normalized);
        return new Point(x, y);
    }
}
