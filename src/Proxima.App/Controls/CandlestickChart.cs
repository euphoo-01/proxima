using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Proxima.App.ViewModels;

namespace Proxima.App.Controls;

public sealed class CandlestickChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<CandlestickPointViewModel>?> CandlesProperty =
        AvaloniaProperty.Register<CandlestickChart, IReadOnlyList<CandlestickPointViewModel>?>(nameof(Candles));

    public IReadOnlyList<CandlestickPointViewModel>? Candles
    {
        get => GetValue(CandlesProperty);
        set => SetValue(CandlesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        IReadOnlyList<CandlestickPointViewModel>? candles = Candles;
        if (candles is null || candles.Count == 0 || Bounds.Width <= 12 || Bounds.Height <= 12)
        {
            return;
        }

        decimal min = candles.Min(static candle => candle.Low);
        decimal max = candles.Max(static candle => candle.High);
        decimal range = max - min;
        if (range <= 0m)
        {
            range = 1m;
        }

        double width = Bounds.Width;
        double height = Bounds.Height;
        double slot = width / candles.Count;
        double bodyWidth = Math.Max(3, slot * 0.55);

        Pen wickPen = new(Brushes.SlateGray, 1);
        Pen upPen = new(new SolidColorBrush(Color.Parse("#148A57")), 1);
        Pen downPen = new(new SolidColorBrush(Color.Parse("#C94949")), 1);
        IBrush upFill = new SolidColorBrush(Color.Parse("#1FA971"));
        IBrush downFill = new SolidColorBrush(Color.Parse("#D85C5C"));

        for (int i = 0; i < candles.Count; i++)
        {
            CandlestickPointViewModel candle = candles[i];
            double x = i * slot + (slot - bodyWidth) / 2d;
            double center = x + bodyWidth / 2d;
            double yHigh = Map(candle.High, min, range, height);
            double yLow = Map(candle.Low, min, range, height);
            double yOpen = Map(candle.Open, min, range, height);
            double yClose = Map(candle.Close, min, range, height);
            context.DrawLine(wickPen, new Point(center, yHigh), new Point(center, yLow));

            bool isUp = candle.Close >= candle.Open;
            double top = Math.Min(yOpen, yClose);
            double bodyHeight = Math.Max(2, Math.Abs(yClose - yOpen));
            Rect body = new(x, top, bodyWidth, bodyHeight);
            context.DrawRectangle(isUp ? upFill : downFill, isUp ? upPen : downPen, body);
        }
    }

    private static double Map(decimal value, decimal min, decimal range, double height)
    {
        double normalized = (double)((value - min) / range);
        return (height - 2d) * (1d - normalized) + 1d;
    }
}
