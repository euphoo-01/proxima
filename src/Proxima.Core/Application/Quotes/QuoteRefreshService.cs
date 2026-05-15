using Proxima.Core.Application.Assets;
using Proxima.Core.Domain.Assets;

namespace Proxima.Core.Application.Quotes;

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
            if (asset.Type is AssetType.Cash || IsLegacyPlainCurrencyPosition(asset))
            {
                CashQuoteRefreshResult cashQuote = await RefreshCashQuoteAsync(asset, cancellationToken).ConfigureAwait(false);
                if (cashQuote.Succeeded)
                {
                    Asset patched = asset with
                    {
                        Ticker = cashQuote.Ticker,
                        Currency = "USD",
                        CurrentPrice = cashQuote.Price,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    };

                    await assets.UpdateAsync(patched, cancellationToken).ConfigureAwait(false);
                    await cache.UpsertLatestAsync(new QuoteCacheEntry(
                        asset.Id,
                        cashQuote.Ticker,
                        cashQuote.Price,
                        "USD",
                        cashQuote.Timestamp,
                        cashQuote.Source), cancellationToken).ConfigureAwait(false);

                    if (cashQuote.FromProvider)
                    {
                        updated++;
                    }
                    else
                    {
                        cached++;
                    }

                    continue;
                }

                failed++;
                errors.Add(cashQuote.Message);

                QuoteCacheEntry? cashEntry = await cache.FindLatestByAssetIdAsync(asset.Id, cancellationToken).ConfigureAwait(false);
                if (cashEntry is not null)
                {
                    Asset patched = asset with
                    {
                        Currency = "USD",
                        CurrentPrice = cashEntry.Price,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    };
                    await assets.UpdateAsync(patched, cancellationToken).ConfigureAwait(false);
                    cached++;
                    failed--;
                }

                continue;
            }

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


    private async Task<CashQuoteRefreshResult> RefreshCashQuoteAsync(Asset asset, CancellationToken cancellationToken)
    {
        string cashCode = NormalizeCashTicker(asset.Ticker, asset.Currency);
        if (string.IsNullOrWhiteSpace(cashCode))
        {
            return CashQuoteRefreshResult.Failure(asset.Ticker, $"{asset.Ticker}: не удалось определить код валюты наличности.");
        }

        if (cashCode.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            return CashQuoteRefreshResult.Success("USD", 1m, DateTimeOffset.UtcNow, "base-currency", fromProvider: false);
        }

        string pair = $"{cashCode}/USD";
        QuoteProviderResult quote = await provider.GetLatestQuoteAsync(pair, "USD", cancellationToken).ConfigureAwait(false);
        if (quote.Succeeded && quote.Quote is not null && quote.Quote.Price > 0m)
        {
            return CashQuoteRefreshResult.Success(cashCode, quote.Quote.Price, quote.Quote.Timestamp, quote.Quote.Source, fromProvider: true);
        }

        string message = string.IsNullOrWhiteSpace(quote.Message)
            ? $"{cashCode}: Twelve Data не вернул курс {pair}."
            : $"{cashCode}: {quote.Message}";

        return CashQuoteRefreshResult.Failure(cashCode, message);
    }

    private static bool IsLegacyPlainCurrencyPosition(Asset asset)
    {
        if (asset.Type is not AssetType.Currency)
        {
            return false;
        }

        string ticker = NormalizeCashTicker(asset.Ticker, asset.Currency);
        return ticker.Length == 3 && ticker.All(static c => c is >= 'A' and <= 'Z');
    }

    private static string NormalizeCashTicker(string ticker, string currency)
    {
        string value = string.IsNullOrWhiteSpace(ticker) ? currency : ticker;
        value = value.Trim().ToUpperInvariant();

        int slashIndex = value.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex > 0)
        {
            value = value[..slashIndex];
        }

        return value;
    }

    private readonly record struct CashQuoteRefreshResult(
        bool Succeeded,
        string Ticker,
        decimal Price,
        DateTimeOffset Timestamp,
        string Source,
        bool FromProvider,
        string Message)
    {
        public static CashQuoteRefreshResult Success(string ticker, decimal price, DateTimeOffset timestamp, string source, bool fromProvider)
        {
            return new CashQuoteRefreshResult(true, ticker, price, timestamp, source, fromProvider, string.Empty);
        }

        public static CashQuoteRefreshResult Failure(string ticker, string message)
        {
            return new CashQuoteRefreshResult(false, ticker, 0m, DateTimeOffset.UtcNow, string.Empty, false, message);
        }
    }

    private static string BuildMessage(int updated, int cached, int failed, IReadOnlyList<string> errors)
    {
        if (updated == 0 && cached == 0 && failed > 0)
        {
            return errors.Count > 0
                ? errors[0]
                : "Не удалось обновить котировки через Twelve Data.";
        }

        if (failed > 0)
        {
            string suffix = errors.Count > 0 ? $" Последняя ошибка: {errors[0]}" : string.Empty;
            return $"Котировки обновлены частично: обновлено={updated}, взято из кэша={cached}, ошибок={failed}.{suffix}";
        }

        return $"Котировки обновлены: обновлено={updated}, взято из кэша={cached}.";
    }
}
