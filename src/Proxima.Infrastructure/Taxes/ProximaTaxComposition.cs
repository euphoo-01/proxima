using Proxima.Application.Auth;
using Proxima.Application.Settings;
using Proxima.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public static class ProximaTaxComposition
{
    public static ITaxCalculator CreateTaxCalculator(
        ICurrentUserContext currentUser,
        ISettingsService settingsService,
        HttpClient httpClient)
    {
        return new DraftTaxCalculator(new ConfigurableExchangeRateProvider(currentUser, settingsService, httpClient));
    }
}
