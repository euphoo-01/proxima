using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Goals;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Goals;

public static class GoalComposition
{
    public static IServiceCollection AddGoalModule(this IServiceCollection services)
    {
        services.AddSingleton<IGoalRepository, PostgresGoalRepository>();
        services.AddSingleton<IGoalService, GoalService>();
        return services;
    }
}
