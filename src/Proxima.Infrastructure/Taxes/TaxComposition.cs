using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public static class TaxComposition
{
    public static IServiceCollection AddTaxModule(this IServiceCollection services)
    {
        services.AddSingleton<NbrbExchangeRateProvider>();
        services.AddSingleton<BelarusbankExchangeRateProvider>();
        services.AddSingleton<MockNbrbExchangeRateProvider>();
        services.AddSingleton<IExchangeRateSource>(sp => new CachingExchangeRateSource(sp.GetRequiredService<NbrbExchangeRateProvider>()));
        services.AddSingleton<IExchangeRateSource>(sp => new CachingExchangeRateSource(sp.GetRequiredService<BelarusbankExchangeRateProvider>()));
        services.AddSingleton<IExchangeRateSource>(sp => new CachingExchangeRateSource(sp.GetRequiredService<MockNbrbExchangeRateProvider>()));
        services.AddSingleton<IExchangeRateProvider, ConfigurableExchangeRateProvider>();
        services.AddSingleton<ITaxCalculator, DraftTaxCalculator>();
        services.AddSingleton<ITaxService, TaxService>();
        return services;
    }
}
