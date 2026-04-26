using Avalonia;

namespace Proxima.App.Controls;

public class MetricCard : BentoCard
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Value));

    public static readonly StyledProperty<string?> DeltaProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Delta));

    public static readonly StyledProperty<string> DeltaKindProperty =
        AvaloniaProperty.Register<MetricCard, string>(nameof(DeltaKind), "Neutral");

    public static readonly StyledProperty<string?> FooterProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Footer));

    public static readonly StyledProperty<object?> SparklineDataProperty =
        AvaloniaProperty.Register<MetricCard, object?>(nameof(SparklineData));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Delta
    {
        get => GetValue(DeltaProperty);
        set => SetValue(DeltaProperty, value);
    }

    public string DeltaKind
    {
        get => GetValue(DeltaKindProperty);
        set => SetValue(DeltaKindProperty, value);
    }

    public string? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public object? SparklineData
    {
        get => GetValue(SparklineDataProperty);
        set => SetValue(SparklineDataProperty, value);
    }
}
