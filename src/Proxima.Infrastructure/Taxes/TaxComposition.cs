using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public static class TaxComposition
{
    public static IServiceCollection AddTaxModule(this IServiceCollection services)
    {
        services.AddSingleton<IExchangeRateProvider, ConfigurableExchangeRateProvider>();
        services.AddSingleton<ITaxCalculator, DraftTaxCalculator>();
        return services;
    }
}
