using Avalonia.Controls;
using Avalonia.Media;

namespace Proxima.App.DesignSystem.Charts;

public static class ProximaChartTheme
{
    public const double AxisLabelFontSize = 11;
    public const double TooltipFontSize = 12;
    public const double AxisStrokeThickness = 1;
    public const double GridStrokeThickness = 1;
    public const double SeriesStrokeThickness = 2;

    public static IBrush ResolveBrush(Control control, string key)
    {
        if (control.TryFindResource(key, out object? value) && value is IBrush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }
}
