using System.Text.Json;
using Proxima.Application.Portfolios;
using Proxima.Domain.Portfolios;

namespace Proxima.Infrastructure.Portfolios;

public sealed class JsonPortfolioRepository(string filePath) : IPortfolioRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public Task<IReadOnlyList<Portfolio>> ListByOwnerAsync(Guid ownerUserId, bool includeArchived, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<Portfolio> query = LoadPortfolios().Where(portfolio => portfolio.OwnerUserId == ownerUserId);
        if (!includeArchived)
        {
            query = query.Where(portfolio => !portfolio.IsArchived);
        }

        return Task.FromResult<IReadOnlyList<Portfolio>>(query.ToArray());
    }

    public Task<Portfolio?> FindByIdAsync(Guid ownerUserId, Guid portfolioId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Portfolio? portfolio = LoadPortfolios().FirstOrDefault(item => item.OwnerUserId == ownerUserId && item.Id == portfolioId);
        return Task.FromResult(portfolio);
    }

    public Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<Portfolio> items = LoadPortfolios();
        items.Add(portfolio);
        SavePortfolios(items);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Portfolio portfolio, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<Portfolio> items = LoadPortfolios();
        int index = items.FindIndex(item => item.OwnerUserId == portfolio.OwnerUserId && item.Id == portfolio.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Portfolio not found.");
        }

        items[index] = portfolio;
        SavePortfolios(items);
        return Task.CompletedTask;
    }

    private List<Portfolio> LoadPortfolios()
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

        return JsonSerializer.Deserialize<List<Portfolio>>(json, JsonOptions) ?? [];
    }

    private void SavePortfolios(IReadOnlyCollection<Portfolio> items)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempFile = filePath + ".tmp";
        string json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(tempFile, json);
        File.Move(tempFile, filePath, overwrite: true);
    }
}
