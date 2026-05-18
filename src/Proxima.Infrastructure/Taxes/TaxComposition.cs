using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public static class TaxComposition
{
    public static IServiceCollection AddTaxModule(this IServiceCollection services)
    {
        services.AddSingleton<IExchangeRateSource, NbrbExchangeRateProvider>();
        services.AddSingleton<IExchangeRateSource, BelarusbankExchangeRateProvider>();
        services.AddSingleton<IExchangeRateSource, MockNbrbExchangeRateProvider>();
        services.AddSingleton<IExchangeRateProvider, ConfigurableExchangeRateProvider>();
        services.AddSingleton<ITaxCalculator, DraftTaxCalculator>();
        services.AddSingleton<ITaxService, TaxService>();
        return services;
    }
}
