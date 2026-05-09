using Proxima.Application.Assets;
using Proxima.Domain.Assets;

namespace Proxima.Application.Quotes;

public sealed class QuoteRefreshService(
    IAssetRepository assets,
    IQuoteProvider provider,
    IQuoteCacheRepository cache) : IQuoteRefreshService
{
    public async Task<QuoteRefreshSummary> RefreshPortfolioAsync(Guid portfolioId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Asset> list = await assets.ListByPortfolioAsync(portfolioId, includeArchived: false, cancellationToken).ConfigureAwait(false);
        int updated = 0;
        int cached = 0;
        int failed = 0;
        List<string> errors = new();

        foreach (Asset asset in list)
        {
            QuoteProviderResult quote = await provider.GetLatestQuoteAsync(asset.Ticker, asset.Currency, cancellationToken).ConfigureAwait(false);
            if (quote.Succeeded && quote.Quote is not null)
            {
                Asset patched = asset with
                {
                    CurrentPrice = quote.Quote.Price,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };
                await assets.UpdateAsync(patched, cancellationToken).ConfigureAwait(false);
                await cache.UpsertLatestAsync(new QuoteCacheEntry(
                    asset.Id,
                    asset.Ticker,
                    quote.Quote.Price,
                    quote.Quote.Currency,
                    quote.Quote.Timestamp,
                    quote.Quote.Source), cancellationToken).ConfigureAwait(false);
                updated++;
                continue;
            }

            failed++;
            if (!string.IsNullOrWhiteSpace(quote.Message))
            {
                errors.Add($"{asset.Ticker}: {quote.Message}");
            }

            if (quote.ErrorKind == QuoteProviderErrorKind.Unauthorized)
            {
                continue;
            }

            QuoteCacheEntry? entry = await cache.FindLatestByAssetIdAsync(asset.Id, cancellationToken).ConfigureAwait(false);
            if (entry is not null)
            {
                Asset patched = asset with
                {
                    CurrentPrice = entry.Price,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };
                await assets.UpdateAsync(patched, cancellationToken).ConfigureAwait(false);
                cached++;
                failed--;
            }
        }

        string message = BuildMessage(updated, cached, failed, errors);
        return new QuoteRefreshSummary(updated, cached, failed, message);
    }

    private static string BuildMessage(int updated, int cached, int failed, IReadOnlyList<string> errors)
    {
        if (updated == 0 && cached == 0 && failed > 0)
        {
            return errors.Count > 0
                ? errors[0]
                : "Не удалось обновить котировки через Finnhub.";
        }

        if (failed > 0)
        {
            string suffix = errors.Count > 0 ? $" Последняя ошибка: {errors[0]}" : string.Empty;
            return $"Котировки обновлены частично: обновлено={updated}, взято из кэша={cached}, ошибок={failed}.{suffix}";
        }

        return $"Котировки обновлены: обновлено={updated}, взято из кэша={cached}.";
    }
}
