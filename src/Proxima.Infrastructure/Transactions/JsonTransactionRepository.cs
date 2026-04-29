using System.Text.Json;
using Proxima.Application.Transactions;
using Proxima.Domain.Transactions;

namespace Proxima.Infrastructure.Transactions;

public sealed class JsonTransactionRepository(string filePath) : ITransactionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public Task<IReadOnlyList<PortfolioTransaction>> ListByPortfolioAsync(Guid portfolioId, bool includeArchived, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<PortfolioTransaction> query = Load().Where(item => item.PortfolioId == portfolioId);
        if (!includeArchived)
        {
            query = query.Where(item => !item.IsArchived);
        }

        return Task.FromResult<IReadOnlyList<PortfolioTransaction>>(query.ToArray());
    }

    public Task<PortfolioTransaction?> FindByIdAsync(Guid portfolioId, Guid transactionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PortfolioTransaction? transaction = Load().FirstOrDefault(item => item.PortfolioId == portfolioId && item.Id == transactionId);
        return Task.FromResult(transaction);
    }

    public Task AddAsync(PortfolioTransaction transaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<PortfolioTransaction> items = Load();
        items.Add(transaction);
        Save(items);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PortfolioTransaction transaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<PortfolioTransaction> items = Load();
        int index = items.FindIndex(item => item.PortfolioId == transaction.PortfolioId && item.Id == transaction.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Transaction not found.");
        }

        items[index] = transaction;
        Save(items);
        return Task.CompletedTask;
    }

    private List<PortfolioTransaction> Load()
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

        return JsonSerializer.Deserialize<List<PortfolioTransaction>>(json, JsonOptions) ?? [];
    }

    private void Save(IReadOnlyCollection<PortfolioTransaction> items)
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
