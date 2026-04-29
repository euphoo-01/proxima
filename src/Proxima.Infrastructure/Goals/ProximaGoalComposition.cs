using Proxima.Application.Goals;

namespace Proxima.Infrastructure.Goals;

public static class ProximaGoalComposition
{
    public static IGoalService CreateGoalService(string goalsStorePath)
    {
        return new GoalService(new JsonGoalRepository(goalsStorePath));
    }

    public static string GetDefaultGoalsStorePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Proxima", "goal-store.json");
    }
}
