using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Proxima.App.CustomControls;

public class BentoCard : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<BentoCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<BentoCard, string?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<BentoCard, object?>(nameof(Icon));

    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<BentoCard, IBrush?>(nameof(Accent));

    public static readonly StyledProperty<string> SizeVariantProperty =
        AvaloniaProperty.Register<BentoCard, string>(nameof(SizeVariant), "Default");

    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<BentoCard, bool>(nameof(IsInteractive));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<BentoCard, ICommand?>(nameof(Command));

    public static readonly StyledProperty<IBrush?> CardBackgroundProperty =
        AvaloniaProperty.Register<BentoCard, IBrush?>(nameof(CardBackground));

    public static readonly StyledProperty<IBrush?> CardBorderBrushProperty =
        AvaloniaProperty.Register<BentoCard, IBrush?>(nameof(CardBorderBrush));

    public static readonly StyledProperty<Thickness> CardBorderThicknessProperty =
        AvaloniaProperty.Register<BentoCard, Thickness>(nameof(CardBorderThickness), new Thickness(0));

    public static readonly StyledProperty<Thickness> CardPaddingProperty =
        AvaloniaProperty.Register<BentoCard, Thickness>(nameof(CardPadding), new Thickness(24));

    public static readonly StyledProperty<CornerRadius> CardCornerRadiusProperty =
        AvaloniaProperty.Register<BentoCard, CornerRadius>(nameof(CardCornerRadius), new CornerRadius(16));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IBrush? Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public string SizeVariant
    {
        get => GetValue(SizeVariantProperty);
        set => SetValue(SizeVariantProperty, value);
    }

    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public IBrush? CardBackground
    {
        get => GetValue(CardBackgroundProperty);
        set => SetValue(CardBackgroundProperty, value);
    }

    public IBrush? CardBorderBrush
    {
        get => GetValue(CardBorderBrushProperty);
        set => SetValue(CardBorderBrushProperty, value);
    }

    public Thickness CardBorderThickness
    {
        get => GetValue(CardBorderThicknessProperty);
        set => SetValue(CardBorderThicknessProperty, value);
    }

    public Thickness CardPadding
    {
        get => GetValue(CardPaddingProperty);
        set => SetValue(CardPaddingProperty, value);
    }

    public CornerRadius CardCornerRadius
    {
        get => GetValue(CardCornerRadiusProperty);
        set => SetValue(CardCornerRadiusProperty, value);
    }
}
