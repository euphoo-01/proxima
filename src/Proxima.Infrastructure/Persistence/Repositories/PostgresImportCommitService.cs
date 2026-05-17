using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Proxima.Core.Application.MarketData;
using Proxima.Core.Application.Transactions;
using Proxima.Core.Domain.Assets;
using Proxima.Core.Domain.Transactions;

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
                PortfolioEntity? portfolio = await ctx.Portfolios
                    .FirstOrDefaultAsync(x => x.Id == portfolioId && !x.IsArchived, cancellationToken)
                    .ConfigureAwait(false);

                if (portfolio is null)
                {
                    return new ImportCommitResult(false, 0, "Портфель не найден.");
                }

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
                HashSet<string> affectedTickers = new(StringComparer.OrdinalIgnoreCase);
                HashSet<string> affectedTags = new(StringComparer.OrdinalIgnoreCase);
                Dictionary<TransactionType, int> savedByType = new();

                foreach (ImportTransactionDraft row in rows)
                {
                    string displayText = row.AssetTicker.Trim();
                    AssetType inferredType = InferAssetType(displayText, BuildInferenceText(row));
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

                    string[] tags = ResolveImportTags(row, inferredType);
                    await AppendAssetTagsAsync(ctx, asset.Id, tags, cancellationToken).ConfigureAwait(false);

                    foreach (string tag in tags)
                    {
                        affectedTags.Add(tag);
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
                        Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(),
                        IsArchived = false,
                        CreatedAt = now,
                        UpdatedAt = now,
                    });

                    savedRows++;
                    affectedTickers.Add(normalizedTicker);
                    savedByType[row.Type] = savedByType.TryGetValue(row.Type, out int count) ? count + 1 : 1;
                }

                if (savedRows > 0)
                {
                    AppendImportAuditLog(ctx, portfolio.OwnerUserId, portfolioId, savedRows, affectedTickers, affectedTags, savedByType, now);
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

    private static async Task AppendAssetTagsAsync(ProximaDbContext ctx, Guid assetId, IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        string[] normalizedTags = tags
            .Select(NormalizeTag)
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string tagName in normalizedTags)
        {
            TagEntity tag = await FindOrCreateTagAsync(ctx, tagName, cancellationToken).ConfigureAwait(false);
            bool trackedLinkExists = ctx.AssetTags.Local.Any(x => x.AssetId == assetId && x.TagId == tag.Id);
            if (trackedLinkExists)
            {
                continue;
            }

            bool storedLinkExists = await ctx.AssetTags
                .AnyAsync(x => x.AssetId == assetId && x.TagId == tag.Id, cancellationToken)
                .ConfigureAwait(false);

            if (!storedLinkExists)
            {
                ctx.AssetTags.Add(new AssetTagEntity
                {
                    AssetId = assetId,
                    TagId = tag.Id,
                });
            }
        }
    }

    private static async Task<TagEntity> FindOrCreateTagAsync(ProximaDbContext ctx, string tagName, CancellationToken cancellationToken)
    {
        string normalizedName = NormalizeTag(tagName);
        TagEntity? tracked = ctx.Tags.Local.FirstOrDefault(x => string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (tracked is not null)
        {
            return tracked;
        }

        string lookup = normalizedName.ToLowerInvariant();
        TagEntity? stored = await ctx.Tags
            .FirstOrDefaultAsync(x => x.Name.ToLower() == lookup, cancellationToken)
            .ConfigureAwait(false);

        if (stored is not null)
        {
            if (!string.Equals(stored.Name, normalizedName, StringComparison.Ordinal))
            {
                stored.Name = normalizedName;
            }

            return stored;
        }

        TagEntity created = new()
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
        };
        ctx.Tags.Add(created);
        return created;
    }

    private static string[] ResolveImportTags(ImportTransactionDraft row, AssetType inferredType)
    {
        List<string> tags = [];

        string[] explicitTags = SplitAndNormalizeTags(row.Tag);
        if (explicitTags.Length > 0)
        {
            tags.AddRange(explicitTags);
        }
        else
        {
            string? legacyTag = NormalizeLegacyTag(row.Notes);
            if (!string.IsNullOrWhiteSpace(legacyTag))
            {
                tags.Add(legacyTag);
            }
        }

        if (tags.Count == 0)
        {
            tags.Add(DefaultTagFor(inferredType));
        }

        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] SplitAndNormalizeTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        char[] separators = [',', ';', '|'];
        return value
            .Split(separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeTag)
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildInferenceText(ImportTransactionDraft row)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(row.Tag))
        {
            parts.Add(row.Tag);
        }

        if (!string.IsNullOrWhiteSpace(row.Notes))
        {
            parts.Add(row.Notes);
        }

        return string.Join(' ', parts);
    }

    private static string? NormalizeLegacyTag(string? value)
    {
        string tag = NormalizeTag(value);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        return IsKnownTag(tag) ? tag : null;
    }

    private static string NormalizeTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string tag = Regex.Replace(value.Trim(), "\\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(50));
        return tag.ToLowerInvariant() switch
        {
            "stock" or "stocks" or "equity" or "equities" or "share" or "shares" or "акции" or "акция" => "Акции",
            "etf" or "фонды" or "фонд" => "ETF",
            "crypto" or "cryptocurrency" or "крипта" or "криптовалюта" or "криптовалюты" => "Криптовалюта",
            "bond" or "bonds" or "облигация" or "облигации" => "Облигации",
            "currency" or "currencies" or "fx" or "валюта" or "валюты" => "Валюта",
            "cash" or "наличные" or "наличность" => "Наличность",
            "dividend" or "dividends" or "дивиденд" or "дивиденды" => "Дивиденды",
            _ => tag,
        };
    }

    private static bool IsKnownTag(string tag)
    {
        return tag is "Акции" or "ETF" or "Криптовалюта" or "Облигации" or "Валюта" or "Наличность" or "Дивиденды";
    }

    private static string DefaultTagFor(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Etf => "ETF",
            AssetType.Crypto => "Криптовалюта",
            AssetType.Bond => "Облигации",
            AssetType.Currency => "Валюта",
            AssetType.Cash => "Наличность",
            _ => "Акции",
        };
    }

    private static void AppendImportAuditLog(
        ProximaDbContext ctx,
        Guid ownerUserId,
        Guid portfolioId,
        int savedRows,
        IReadOnlyCollection<string> affectedTickers,
        IReadOnlyCollection<string> affectedTags,
        IReadOnlyDictionary<TransactionType, int> savedByType,
        DateTimeOffset occurredAtUtc)
    {
        string metadata = JsonSerializer.Serialize(new
        {
            portfolioId,
            savedRows,
            assetCount = affectedTickers.Count,
            tagCount = affectedTags.Count,
            tickers = affectedTickers.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase).Take(25).ToArray(),
            tags = affectedTags.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase).Take(25).ToArray(),
            transactionTypes = savedByType
                .OrderBy(static x => x.Key.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key.ToString(), x => x.Value),
        });

        ctx.AuditLog.Add(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            UserId = ownerUserId,
            Action = "Import.Committed",
            Timestamp = occurredAtUtc,
            MetadataJson = metadata,
        });
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
