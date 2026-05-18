using System.Globalization;
using System.Windows.Input;
using Avalonia.Media;
using Proxima.App.ViewModels;
using Proxima.App.Common.Commands;

namespace Proxima.App.Views.Dashboard;

public sealed class DashboardTimeframeViewModel : ViewModelBase
{
    private bool _isSelected;

    public DashboardTimeframeViewModel(string label, bool isSelected)
    {
        Label = label;
        _isSelected = isSelected;
    }

    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed record DashboardBarViewModel(
    string Label,
    double Height,
    bool IsActive,
    bool IsTooltipVisible,
    string TooltipText);

public sealed class DashboardAllocationRowViewModel
{
    public DashboardAllocationRowViewModel(string label, decimal value, decimal percent, string markerColor)
    {
        Label = label;
        Value = value;
        Percent = percent;
        MarkerBrush = new SolidColorBrush(Color.Parse(markerColor));
    }

    public string Label { get; }

    public decimal Value { get; }

    public decimal Percent { get; }

    public string PercentText => $"{Percent:0}%";

    public IBrush MarkerBrush { get; }
}

public sealed class DashboardTransactionRowViewModel
{
    private readonly Action _edit;
    private readonly Action _delete;

    public DashboardTransactionRowViewModel(
        Guid id,
        string assetName,
        string dateText,
        string typeText,
        string amountText,
        string iconKind,
        bool isPositiveAmount,
        Action edit,
        Action delete)
    {
        Id = id;
        AssetName = assetName;
        DateText = dateText;
        TypeText = typeText;
        AmountText = amountText;
        IconKind = iconKind;
        IsPositiveAmount = isPositiveAmount;
        _edit = edit;
        _delete = delete;
        EditCommand = new RelayCommand(_ => _edit());
        DeleteCommand = new RelayCommand(_ => _delete());
    }

    public Guid Id { get; }

    public string AssetName { get; }

    public string DateText { get; }

    public string TypeText { get; }

    public string AmountText { get; }

    public string IconKind { get; }

    public bool IsPositiveAmount { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public bool IsBitcoin => IconKind.Equals("bitcoin", StringComparison.OrdinalIgnoreCase);

    public bool IsSp => IconKind.Equals("sp", StringComparison.OrdinalIgnoreCase);

    public bool IsBuy => TypeText.Contains("покуп", StringComparison.OrdinalIgnoreCase);

    public bool IsDividend => TypeText.Contains("дивид", StringComparison.OrdinalIgnoreCase)
        || TypeText.Contains("staking", StringComparison.OrdinalIgnoreCase)
        || TypeText.Contains("airdrop", StringComparison.OrdinalIgnoreCase);

    public bool IsSell => TypeText.Contains("продаж", StringComparison.OrdinalIgnoreCase)
        || TypeText.Contains("комисс", StringComparison.OrdinalIgnoreCase)
        || TypeText.Contains("налог", StringComparison.OrdinalIgnoreCase);

    public string IconText => IconKind.ToLowerInvariant() switch
    {
        "bitcoin" => "₿",
        "sp" => "S&P",
        _ => IconKind.Length <= 3 ? IconKind : IconKind[..3]
    };
}
