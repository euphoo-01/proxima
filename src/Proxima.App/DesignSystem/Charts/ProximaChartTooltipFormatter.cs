using System.Globalization;

namespace Proxima.App.DesignSystem.Charts;

public static class ProximaChartTooltipFormatter
{
    public static string FormatValue(decimal value, ProximaChartValueKind kind)
    {
        return kind switch
        {
            ProximaChartValueKind.Money => string.Format(CultureInfo.CurrentCulture, "{0:N2}", value),
            ProximaChartValueKind.Percent => string.Format(CultureInfo.CurrentCulture, "{0:N2}%", value),
            ProximaChartValueKind.Quantity => string.Format(CultureInfo.CurrentCulture, "{0:N4}", value),
            _ => string.Format(CultureInfo.CurrentCulture, "{0:N2}", value)
        };
    }

    public static string FormatDateTime(DateTimeOffset value)
    {
        return value.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
    }
}
