using Avalonia;
using Avalonia.Controls;

namespace Proxima.App.CustomControls;

public class StatusPill : ContentControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusPill, string?>(nameof(Text));

    public static readonly StyledProperty<string> KindProperty =
        AvaloniaProperty.Register<StatusPill, string>(nameof(Kind), "Neutral");

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }
}
