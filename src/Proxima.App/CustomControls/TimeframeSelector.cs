using Avalonia.Controls;

namespace Proxima.App.CustomControls;

public class TimeframeSelector : ComboBox
{
    public TimeframeSelector()
    {
        ItemsSource = new[] { "1D", "1W", "1M", "6M", "1Y", "ALL" };
        SelectedIndex = 2;
    }
}
