using Proxima.Core.Domain.Goals;

namespace Proxima.Core.Application.Goals;

public sealed class GoalService(IGoalRepository repository) : IGoalService
{
    private const string GoalCurrency = "USD";
    public Task<IReadOnlyList<Goal>> ListActiveAsync(Guid portfolioId, CancellationToken cancellationToken = default)
    {
        return repository.ListByPortfolioAsync(portfolioId, includeArchived: false, cancellationToken);
    }

    public async Task<GoalOperationResult> CreateAsync(CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        string? validation = Validate(request.Title, request.TargetAmount, request.MonthlyContribution);
        if (validation is not null)
        {
            return GoalOperationResult.Failure(validation);
        }

        Goal goal = new(
            Guid.NewGuid(),
            request.PortfolioId,
            request.Title.Trim(),
            request.TargetAmount,
            GoalCurrency,
            request.MonthlyContribution,
            request.ExpectedAnnualReturnPercent,
            request.TargetDate,
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        await repository.AddAsync(goal, cancellationToken).ConfigureAwait(false);
        return GoalOperationResult.Success(goal);
    }

    public async Task<GoalOperationResult> UpdateAsync(UpdateGoalRequest request, CancellationToken cancellationToken = default)
    {
        string? validation = Validate(request.Title, request.TargetAmount, request.MonthlyContribution);
        if (validation is not null)
        {
            return GoalOperationResult.Failure(validation);
        }

        Goal? existing = await repository.FindByIdAsync(request.PortfolioId, request.GoalId, cancellationToken).ConfigureAwait(false);
        if (existing is null || existing.IsArchived)
        {
            return GoalOperationResult.Failure("Цель не найдена.");
        }

        Goal updated = existing with
        {
            Title = request.Title.Trim(),
            TargetAmount = request.TargetAmount,
            Currency = GoalCurrency,
            MonthlyContribution = request.MonthlyContribution,
            ExpectedAnnualReturnPercent = request.ExpectedAnnualReturnPercent,
            TargetDate = request.TargetDate,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return GoalOperationResult.Success(updated);
    }

    public async Task<GoalOperationResult> ArchiveAsync(Guid portfolioId, Guid goalId, CancellationToken cancellationToken = default)
    {
        Goal? existing = await repository.FindByIdAsync(portfolioId, goalId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return GoalOperationResult.Failure("Цель не найдена.");
        }

        Goal archived = existing with { IsArchived = true, UpdatedAt = DateTimeOffset.UtcNow };
        await repository.UpdateAsync(archived, cancellationToken).ConfigureAwait(false);
        return GoalOperationResult.Success(archived);
    }

    public GoalForecast Forecast(Goal goal, decimal currentPortfolioValue)
    {
        decimal value = Math.Max(0m, currentPortfolioValue);
        decimal target = goal.TargetAmount;
        decimal monthlyRate = (goal.ExpectedAnnualReturnPercent ?? 0m) / 12m / 100m;
        if (monthlyRate < -0.5m)
        {
            return new GoalForecast(false, 0, null, value, "Слишком негативная ожидаемая доходность.");
        }

        if (target <= value)
        {
            return new GoalForecast(true, 0, DateTimeOffset.UtcNow, value, "Цель уже достигнута.");
        }

        for (int month = 1; month <= 600; month++)
        {
            value *= 1m + monthlyRate;
            value += goal.MonthlyContribution;
            if (value >= target)
            {
                return new GoalForecast(true, month, DateTimeOffset.UtcNow.AddMonths(month), value, "Прогноз обновлён.");
            }
        }

        return new GoalForecast(false, 600, null, value, "При текущих параметрах цель не достигается за 50 лет.");
    }

    private static string? Validate(string title, decimal target, decimal monthlyContribution)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Название цели обязательно.";
        }

        if (target <= 0m)
        {
            return "Целевая сумма должна быть положительной.";
        }

        if (monthlyContribution < 0m)
        {
            return "Ежемесячный взнос не может быть отрицательным.";
        }

        return null;
    }
}
