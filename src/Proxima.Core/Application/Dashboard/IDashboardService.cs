namespace Proxima.Core.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardOverview> GetOverviewAsync(Guid portfolioId, CancellationToken cancellationToken = default);
}
