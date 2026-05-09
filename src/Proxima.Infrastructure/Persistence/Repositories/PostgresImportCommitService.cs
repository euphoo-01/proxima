using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Proxima.Application.Transactions;
using Proxima.Application.MarketData;
using Proxima.Domain.Assets;
using Proxima.Domain.Transactions;

namespace Proxima.Infrastructure.Persistence.Repositories;

public sealed class PostgresImportCommitService(IProximaUnitOfWorkFactory uowFactory) : IImportCommitService
{
    public async Task<ImportCommitResult> CommitAsync(Guid portfolioId, IReadOnlyList<ImportTransactionDraft> rows, CancellationToken cancellationToken = default)
    {
        if (portfolioId == Guid.Empty)
        {
            return new ImportCommitResult(false, 0, "Портфель обязателен.");
        }

        if (rows.Count == 0)
        {
            return new ImportCommitResult(true, 0, "Нет строк для импорта.");
        }

        try
        {
            return await uowFactory.ExecuteInTransactionAsync(async uow =>
            {
                ProximaDbContext ctx = uow.Context;
                List<AssetEntity> assets = await ctx.Assets
                    .Where(x => x.PortfolioId == portfolioId && !x.IsArchived)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                Dictionary<string, AssetEntity> assetByTicker = assets
                    .GroupBy(x => NormalizeTicker(x.Ticker, ParseAssetType(x.Type)), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToDictionary(x => NormalizeTicker(x.Ticker, ParseAssetType(x.Type)), StringComparer.OrdinalIgnoreCase);

                DateTimeOffset now = DateTimeOffset.UtcNow;
                int savedRows = 0;

                foreach (ImportTransactionDraft row in rows)
                {
                    string displayText = row.AssetTicker.Trim();
                    AssetType inferredType = InferAssetType(displayText, row.Notes);
                    string normalizedTicker = NormalizeTicker(displayText, inferredType);
                    if (string.IsNullOrWhiteSpace(normalizedTicker))
                    {
                        continue;
                    }

                    string storageCurrency = ResolveStorageCurrency(inferredType, row.Currency);

                    AssetEntity asset;
                    if (!assetByTicker.TryGetValue(normalizedTicker, out asset!))
                    {
                        asset = new AssetEntity
                        {
                            Id = Guid.NewGuid(),
                            PortfolioId = portfolioId,
                            Ticker = normalizedTicker,
                            Name = ResolveAssetName(displayText, normalizedTicker, inferredType),
                            Type = inferredType.ToString(),
                            Currency = storageCurrency,
                            Quantity = 0m,
                            AverageBuyPrice = 0m,
                            CurrentPrice = row.Price > 0m ? row.Price : 0m,
                            IsArchived = false,
                            CreatedAt = now,
                            UpdatedAt = now,
                        };

                        ctx.Assets.Add(asset);
                        assetByTicker[normalizedTicker] = asset;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(asset.Name))
                        {
                            asset.Name = displayText;
                        }

                        asset.UpdatedAt = now;
                    }

                    ApplyTransactionToAsset(asset, row);

                    decimal gross = row.Quantity * row.Price;
                    ctx.Transactions.Add(new TransactionEntity
                    {
                        Id = Guid.NewGuid(),
                        PortfolioId = portfolioId,
                        AssetId = asset.Id,
                        Type = row.Type.ToString(),
                        TradeDate = NormalizeTradeDateForPostgres(row.TradeDate),
                        Quantity = row.Quantity,
                        Price = row.Price,
                        GrossAmount = gross,
                        FeeAmount = row.FeeAmount,
                        TaxAmount = 0m,
                        Currency = storageCurrency,
                        EncryptedNotes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes,
                        IsArchived = false,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });

                    savedRows++;
                }

                return new ImportCommitResult(true, savedRows, $"Импортировано строк: {savedRows}");
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Exception root = ex.GetBaseException();
            string details = string.Equals(root.Message, ex.Message, StringComparison.Ordinal)
                ? ex.Message
                : $"{ex.Message} Подробно: {root.Message}";

            return new ImportCommitResult(false, 0, $"Импорт отменён: {details}");
        }
    }

    private static DateTimeOffset NormalizeTradeDateForPostgres(DateTimeOffset value)
    {
        if (value.TimeOfDay == TimeSpan.Zero)
        {
            return new DateTimeOffset(value.Year, value.Month, value.Day, 12, 0, 0, TimeSpan.Zero);
        }

        return value.ToUniversalTime();
    }

    private static void ApplyTransactionToAsset(AssetEntity asset, ImportTransactionDraft row)
    {
        if (row.Price > 0m)
        {
            asset.CurrentPrice = row.Price;
        }

        asset.Currency = ResolveStorageCurrency(ParseAssetType(asset.Type), row.Currency);
        asset.UpdatedAt = DateTimeOffset.UtcNow;

        switch (row.Type)
        {
            case TransactionType.Buy:
            {
                decimal existingQuantity = asset.Quantity;
                decimal newQuantity = existingQuantity + row.Quantity;
                decimal weightedTotal = (existingQuantity * asset.AverageBuyPrice) + (row.Quantity * row.Price);

                asset.Quantity = newQuantity;
                asset.AverageBuyPrice = newQuantity <= 0m ? row.Price : weightedTotal / newQuantity;
                break;
            }

            case TransactionType.Sell:
            {
                asset.Quantity = Math.Max(0m, asset.Quantity - row.Quantity);
                if (asset.Quantity == 0m)
                {
                    asset.AverageBuyPrice = 0m;
                }

                break;
            }
        }
    }

    private static string NormalizeTicker(string value, AssetType assetType)
    {
        string normalized = MarketSymbolNormalizer.NormalizeForTwelveData(value);
        string cleaned = Regex.Replace(normalized, "[^A-Z0-9./]", string.Empty, RegexOptions.None, TimeSpan.FromMilliseconds(50));
        string ticker = string.IsNullOrWhiteSpace(cleaned) ? normalized : cleaned;

        if (assetType is AssetType.Cash && ticker.Contains('/', StringComparison.Ordinal))
        {
            ticker = ticker.Split('/')[0];
        }

        return ticker;
    }

    private static string ResolveAssetName(string displayText, string ticker, AssetType assetType)
    {
        return assetType is AssetType.Cash ? $"{ticker} Cash" : displayText;
    }

    private static AssetType ParseAssetType(string value)
    {
        return Enum.TryParse(value, true, out AssetType parsed) ? parsed : AssetType.Stock;
    }

    private static string ResolveStorageCurrency(AssetType assetType, string value)
    {
        return assetType is AssetType.Cash ? "USD" : NormalizeCurrency(value);
    }

    private static string NormalizeCurrency(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "USD"
            : value.Trim().ToUpperInvariant();
    }

    private static AssetType InferAssetType(string value, string? tagOrNotes = null)
    {
        string source = $"{value} {tagOrNotes}".ToLowerInvariant();

        if (source.Contains("binance:", StringComparison.Ordinal)
            || source.Contains("coinbase:", StringComparison.Ordinal)
            || source.Contains("kraken:", StringComparison.Ordinal)
            || source.Contains("btc", StringComparison.Ordinal)
            || source.Contains("bitcoin", StringComparison.Ordinal)
            || source.Contains("eth", StringComparison.Ordinal)
            || source.Contains("ethereum", StringComparison.Ordinal)
            || source.Contains("крип", StringComparison.Ordinal)
            || source.Contains("crypto", StringComparison.Ordinal))
        {
            return AssetType.Crypto;
        }

        if (source.Contains("bond", StringComparison.Ordinal) || source.Contains("облиг", StringComparison.Ordinal))
        {
            return AssetType.Bond;
        }

        if (source.Contains("etf", StringComparison.Ordinal))
        {
            return AssetType.Etf;
        }

        if (source.Contains("cash", StringComparison.Ordinal) || source.Contains("налич", StringComparison.Ordinal))
        {
            return AssetType.Cash;
        }

        if (source.Contains("валют", StringComparison.Ordinal)
            || value.Trim().Equals("USD", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("EUR", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("BYN", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("RUB", StringComparison.OrdinalIgnoreCase))
        {
            return AssetType.Currency;
        }

        return AssetType.Stock;
    }
}
