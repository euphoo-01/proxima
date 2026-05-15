using Proxima.Core.Application.Goals;
using Proxima.Infrastructure.Persistence;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Goals;

public static class ProximaGoalComposition
{
    public static IGoalService CreateGoalService(
        IProximaUnitOfWorkFactory uowFactory,
        IProximaUnitOfWorkAccessor uowAccessor)
    {
        return new GoalService(new PostgresGoalRepository(uowFactory, uowAccessor));
    }
}
