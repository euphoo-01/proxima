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
            }
            else
            {
                failed++;
            }
        }

        return new QuoteRefreshSummary(updated, cached, failed, $"Quotes: updated={updated}, cached={cached}, failed={failed}");
    }
}
