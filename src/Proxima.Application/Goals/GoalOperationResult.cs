using Proxima.Domain.Goals;

namespace Proxima.Application.Goals;

public sealed record GoalOperationResult(bool Succeeded, string Message, Goal? Goal)
{
    public static GoalOperationResult Success(Goal goal) => new(true, string.Empty, goal);
    public static GoalOperationResult Failure(string message) => new(false, message, null);
}
