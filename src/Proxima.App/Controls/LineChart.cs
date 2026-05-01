using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Proxima.App.Controls;

public sealed class LineChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<decimal>?> ValuesProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<decimal>?>(nameof(Values));

    public static readonly StyledProperty<IBrush> StrokeProperty =
        AvaloniaProperty.Register<LineChart, IBrush>(nameof(Stroke), Brushes.DodgerBlue);

    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<LineChart, IBrush>(nameof(Fill), new SolidColorBrush(Color.FromArgb(28, 45, 140, 255)));

    public IReadOnlyList<decimal>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IBrush Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        IReadOnlyList<decimal>? values = Values;
        if (values is null || values.Count < 2 || Bounds.Width <= 8 || Bounds.Height <= 8)
        {
            return;
        }

        decimal min = values.Min();
        decimal max = values.Max();
        decimal range = max - min;
        if (range <= 0m)
        {
            range = 1m;
        }

        double width = Bounds.Width;
        double height = Bounds.Height;
        double xStep = width / Math.Max(1, values.Count - 1);
        double bottom = height - 1;

        StreamGeometry lineGeometry = new();
        StreamGeometry areaGeometry = new();
        using (StreamGeometryContext line = lineGeometry.Open())
        using (StreamGeometryContext area = areaGeometry.Open())
        {
            Point first = ToPoint(0, values[0], min, range, xStep, height);
            line.BeginFigure(first, isFilled: false);
            area.BeginFigure(new Point(first.X, bottom), isFilled: true);
            area.LineTo(first);
            for (int i = 1; i < values.Count; i++)
            {
                Point point = ToPoint(i, values[i], min, range, xStep, height);
                line.LineTo(point);
                area.LineTo(point);
            }

            Point last = ToPoint(values.Count - 1, values[^1], min, range, xStep, height);
            area.LineTo(new Point(last.X, bottom));
            area.EndFigure(isClosed: true);
        }

        context.DrawGeometry(Fill, null, areaGeometry);
        context.DrawGeometry(null, new Pen(Stroke, 2), lineGeometry);
    }

    private static Point ToPoint(int index, decimal value, decimal min, decimal range, double step, double height)
    {
        double x = index * step;
        double normalized = (double)((value - min) / range);
        double y = (height - 2d) * (1d - normalized) + 1d;
        return new Point(x, y);
    }
}
