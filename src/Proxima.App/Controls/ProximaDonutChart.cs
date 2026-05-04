using Avalonia;
using Avalonia.Controls;
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

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (IsLoading)
        {
            DrawText(context, "Загрузка...", ProximaChartTheme.ResolveBrush(this, "ProximaBrush.TextMuted"), 12, 12, 12);
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
            ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Warning"),
            ProximaChartTheme.ResolveBrush(this, "ProximaBrush.Success"),
            ProximaChartTheme.ResolveBrush(this, "ProximaBrush.TextMuted")
        ];

        decimal sum = values.Sum();
        double cx = Bounds.Width / 2d;
        double cy = Bounds.Height / 2d;
        double r = Math.Max(16, Math.Min(Bounds.Width, Bounds.Height) / 2d - 8);
        double inner = r * 0.62;

        double start = -90;
        for (int i = 0; i < values.Count; i++)
        {
            double sweep = sum <= 0m ? 0 : (double)(values[i] / sum * 360m);
            if (sweep <= 0.01)
            {
                continue;
            }

            StreamGeometry arc = BuildSegment(cx, cy, r, inner, start, start + sweep);
            context.DrawGeometry(palette[i % palette.Length], null, arc);
            start += sweep;
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
}
