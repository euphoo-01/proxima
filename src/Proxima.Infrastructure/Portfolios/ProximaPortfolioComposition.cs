using Proxima.Application.Portfolios;

namespace Proxima.Infrastructure.Portfolios;

public static class ProximaPortfolioComposition
{
    public static IPortfolioService CreatePortfolioService(string portfolioStorePath)
    {
        return new PortfolioService(new JsonPortfolioRepository(portfolioStorePath));
    }

    public static string GetDefaultPortfolioStorePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Proxima", "portfolio-store.json");
    }
}
