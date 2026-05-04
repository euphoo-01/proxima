using System.Globalization;

namespace Proxima.App.DesignSystem.Charts;

public enum ProximaChartValueKind
{
    Money,
    Percent,
    DateTime,
    Quantity
}

public sealed record ProximaChartAxisConfig(
    string XAxisTitle,
    string YAxisTitle,
    Func<int, string> XLabeler,
    Func<decimal, string> YLabeler,
    int GridLines = 4);

public static class ProximaChartAxisFactory
{
    public static ProximaChartAxisConfig Create(
        ProximaChartValueKind yKind,
        IReadOnlyList<string>? xLabels = null,
        string xAxisTitle = "Период",
        string? yAxisTitle = null)
    {
        string resolvedYTitle = yAxisTitle ?? yKind switch
        {
            ProximaChartValueKind.Money => "Сумма",
            ProximaChartValueKind.Percent => "%",
            ProximaChartValueKind.DateTime => "Дата",
            _ => "Количество"
        };

        return new ProximaChartAxisConfig(
            xAxisTitle,
            resolvedYTitle,
            index => xLabels is not null && index >= 0 && index < xLabels.Count ? xLabels[index] : index.ToString(CultureInfo.InvariantCulture),
            value => ProximaChartTooltipFormatter.FormatValue(value, yKind));
    }
}
