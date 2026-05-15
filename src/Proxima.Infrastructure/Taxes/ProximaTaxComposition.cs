using Proxima.Core.Application.Auth;
using Proxima.Core.Application.Settings;
using Proxima.Core.Application.Taxes;

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
