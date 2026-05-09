using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Proxima.App.DesignSystem.Charts;

namespace Proxima.App.Controls;

public sealed class ProximaDonutChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<decimal>?> ValuesProperty =
        AvaloniaProperty.Register<ProximaDonutChart, IReadOnlyList<decimal>?>(nameof(Values));

    public static readonly StyledProperty<string?> EmptyStateTextProperty =
        AvaloniaProperty.Register<ProximaDonutChart, string?>(nameof(EmptyStateText), "Нет данных для распределения");

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ProximaDonutChart, bool>(nameof(IsLoading));

    public static readonly StyledProperty<string?> ErrorStateTextProperty =
        AvaloniaProperty.Register<ProximaDonutChart, string?>(nameof(ErrorStateText));

    private IReadOnlyList<(double Start, double End, decimal Value)> _segments = [];
    private int _hoverIndex = -1;

    public IReadOnlyList<decimal>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public string? EmptyStateText
    {
        get => GetValue(EmptyStateTextProperty);
        set => SetValue(EmptyStateTextProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string? ErrorStateText
    {
        get => GetValue(ErrorStateTextProperty);
        set => SetValue(ErrorStateTextProperty, value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_segments.Count == 0)
        {
            _hoverIndex = -1;
            InvalidateVisual();
            return;
        }

        Point p = e.GetPosition(this);
        double cx = Bounds.Width / 2d;
        double cy = Bounds.Height / 2d;
        double dx = p.X - cx;
        double dy = p.Y - cy;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double r = Math.Max(16, Math.Min(Bounds.Width, Bounds.Height) / 2d - 8);
        double inner = r * 0.62;
        if (distance < inner || distance > r)
        {
            _hoverIndex = -1;
            InvalidateVisual();
            return;
        }

        double angle = Math.Atan2(dy, dx) * 180d / Math.PI + 90d;
        if (angle < 0)
        {
            angle += 360d;
        }

        _hoverIndex = -1;
        for (int i = 0; i < _segments.Count; i++)
        {
            (double start, double end, _) = _segments[i];
            if (angle >= start && angle <= end)
            {
                _hoverIndex = i;
                break;
            }
        }

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
        if (values is null || values.Count == 0 || values.All(static v => v <= 0m))
        {
            DrawText(context, EmptyStateText ?? "Нет данных", ProximaChartTheme.ResolveBrush(this, "ProximaBrush.TextMuted"), 12, 12, 12);
            return;
        }

        IBrush[] palette =
        [
            ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Brand"),
            ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Success"),
            new SolidColorBrush(Color.Parse("#B7D5E4")),
            ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Warning"),
            new SolidColorBrush(Color.Parse("#7158E2")),
            ProximaChartTheme.ResolveBrush(this, "ProximaBrush.TextMuted")
        ];

        decimal sum = values.Sum();
        List<(double Start, double End, decimal Value)> segments = [];
        double cx = Bounds.Width / 2d;
        double cy = Bounds.Height / 2d;
        double r = Math.Max(16, Math.Min(Bounds.Width, Bounds.Height) / 2d - 8);
        double inner = r * 0.62;

        double start = 0;
        for (int i = 0; i < values.Count; i++)
        {
            double sweep = sum <= 0m ? 0 : (double)(values[i] / sum * 360m);
            if (sweep <= 0.01)
            {
                continue;
            }

            StreamGeometry arc = BuildSegment(cx, cy, r, inner, start - 90, start + sweep - 90);
            context.DrawGeometry(palette[i % palette.Length], null, arc);

            segments.Add((start, start + sweep, values[i]));
            start += sweep;
        }
        _segments = segments;

        if (_hoverIndex >= 0 && _hoverIndex < _segments.Count && sum > 0m)
        {
            IBrush labelBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.TextSecondary");
            IBrush tooltipBackground = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Surface");
            IBrush borderBrush = ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Border");
            decimal value = _segments[_hoverIndex].Value;
            string text = ProximaChartTooltipFormatter.FormatValue(sum == 0m ? 0m : value / sum * 100m, ProximaChartValueKind.Percent);
            double boxWidth = 96;
            double boxHeight = 26;
            double boxX = Math.Clamp(cx - boxWidth / 2d, 8, Bounds.Width - boxWidth - 8);
            double boxY = Math.Clamp(cy - boxHeight / 2d, 8, Bounds.Height - boxHeight - 8);
            context.DrawRectangle(tooltipBackground, new Pen(borderBrush, 1), new Rect(boxX, boxY, boxWidth, boxHeight), 6, 6);
            DrawText(context, text, labelBrush, boxX + 8, boxY + 6, ProximaChartTheme.TooltipFontSize);
        }
    }

    private static StreamGeometry BuildSegment(double cx, double cy, double outerR, double innerR, double startDeg, double endDeg)
    {
        Point p1 = Polar(cx, cy, outerR, startDeg);
        Point p2 = Polar(cx, cy, outerR, endDeg);
        Point p3 = Polar(cx, cy, innerR, endDeg);
        Point p4 = Polar(cx, cy, innerR, startDeg);

        bool large = endDeg - startDeg > 180;

        StreamGeometry g = new();
        using StreamGeometryContext c = g.Open();
        c.BeginFigure(p1, true);
        c.ArcTo(p2, new Size(outerR, outerR), 0, large, SweepDirection.Clockwise);
        c.LineTo(p3);
        c.ArcTo(p4, new Size(innerR, innerR), 0, large, SweepDirection.CounterClockwise);
        c.EndFigure(true);
        return g;
    }

    private static Point Polar(double cx, double cy, double r, double deg)
    {
        double rad = deg * Math.PI / 180d;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
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

        DrawText(context, text, brush, 12, 12, 12);
    }
}
