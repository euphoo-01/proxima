using Avalonia;
using Avalonia.Controls;

namespace Proxima.App.Controls;

public class DataTableHeaderCell : ContentControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DataTableHeaderCell, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
