using System.Text.Json;
using Proxima.Application.Goals;
using Proxima.Domain.Goals;

namespace Proxima.Infrastructure.Goals;

public sealed class JsonGoalRepository(string filePath) : IGoalRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public Task<IReadOnlyList<Goal>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<Goal> query = Load().Where(item => item.PortfolioId == portfolioId);
        if (!includeArchived)
        {
            query = query.Where(item => !item.IsArchived);
        }

        return Task.FromResult<IReadOnlyList<Goal>>(query.ToArray());
    }

    public Task<Goal?> FindByIdAsync(Guid portfolioId, Guid goalId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Load().FirstOrDefault(item => item.PortfolioId == portfolioId && item.Id == goalId));
    }

    public Task AddAsync(Goal goal, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Goal> items = Load();
        items.Add(goal);
        Save(items);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Goal goal, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Goal> items = Load();
        int index = items.FindIndex(item => item.PortfolioId == goal.PortfolioId && item.Id == goal.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Goal not found.");
        }

        items[index] = goal;
        Save(items);
        return Task.CompletedTask;
    }

    private List<Goal> Load()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        string json = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<Goal>>(json, JsonOptions) ?? [];
    }

    private void Save(IReadOnlyCollection<Goal> items)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tmp = filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(items, JsonOptions));
        File.Move(tmp, filePath, true);
    }
}
