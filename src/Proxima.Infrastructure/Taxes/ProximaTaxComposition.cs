using Proxima.Application.Taxes;
using Proxima.Infrastructure.Settings;

namespace Proxima.Infrastructure.Taxes;

public static class ProximaTaxComposition
{
    public static ITaxCalculator CreateTaxCalculator(string settingsStorePath)
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        return new DraftTaxCalculator(new ConfigurableExchangeRateProvider(new LocalSettingsReader(settingsStorePath), client));
    }
}
