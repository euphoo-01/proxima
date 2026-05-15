using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Portfolios;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Portfolios;

public static class PortfolioComposition
{
    public static IServiceCollection AddPortfolioModule(this IServiceCollection services)
    {
        services.AddSingleton<IPortfolioRepository, PostgresPortfolioRepository>();
        services.AddSingleton<IPortfolioService, PortfolioService>();
        return services;
    }
}
