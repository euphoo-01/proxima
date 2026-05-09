using System.Globalization;
using System.Text.Json;
using Proxima.Application.Quotes;

namespace Proxima.Infrastructure.Quotes;

public sealed class FinnhubQuoteProvider(HttpClient httpClient, string apiKey) : IQuoteProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _apiKey = apiKey;

    public async Task<QuoteProviderResult> GetLatestQuoteAsync(string ticker, string currency, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.Unknown, "Ticker is required.");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.Unauthorized, "Finnhub API key не задан.");
        }

        string normalizedTicker = ticker.Trim().ToUpperInvariant();
        try
        {
            if (TryMapCryptoSymbol(normalizedTicker, out string cryptoSymbol))
            {
                return await GetLatestCryptoQuoteAsync(normalizedTicker, cryptoSymbol, currency, cancellationToken)
                    .ConfigureAwait(false);
            }

            return await GetLatestStockQuoteAsync(normalizedTicker, currency, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.Network, $"Finnhub request failed: {ex.Message}");
        }
    }

    private async Task<QuoteProviderResult> GetLatestStockQuoteAsync(
        string normalizedTicker,
        string currency,
        CancellationToken cancellationToken)
    {
        string safeTicker = Uri.EscapeDataString(normalizedTicker);
        string url = $"https://finnhub.io/api/v1/quote?symbol={safeTicker}&token={Uri.EscapeDataString(_apiKey)}";

        using JsonDocument json = await GetJsonDocumentAsync(url, cancellationToken).ConfigureAwait(false);
        JsonElement root = json.RootElement;
        decimal current = TryGetDecimal(root, "c");
        if (current <= 0m)
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.NotFound, $"Finnhub не вернул цену для {normalizedTicker}.");
        }

        decimal open = TryGetDecimal(root, "o");
        decimal high = TryGetDecimal(root, "h");
        decimal low = TryGetDecimal(root, "l");
        long unix = root.TryGetProperty("t", out JsonElement tEl) && tEl.TryGetInt64(out long epoch)
            ? epoch
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        QuoteOhlc ohlc = new(open <= 0 ? current : open, high <= 0 ? current : high, low <= 0 ? current : low, current);
        QuoteData data = new(
            normalizedTicker,
            current,
            NormalizeCurrency(currency),
            DateTimeOffset.FromUnixTimeSeconds(unix),
            "finnhub",
            ohlc,
            null);

        return QuoteProviderResult.Success(data);
    }

    private async Task<QuoteProviderResult> GetLatestCryptoQuoteAsync(
        string originalTicker,
        string finnhubSymbol,
        string currency,
        CancellationToken cancellationToken)
    {
        DateTimeOffset to = DateTimeOffset.UtcNow;
        DateTimeOffset from = to.AddDays(-2);
        string safeSymbol = Uri.EscapeDataString(finnhubSymbol);
        string url =
            $"https://finnhub.io/api/v1/crypto/candle?symbol={safeSymbol}&resolution=1&from={from.ToUnixTimeSeconds()}&to={to.ToUnixTimeSeconds()}&token={Uri.EscapeDataString(_apiKey)}";

        using JsonDocument json = await GetJsonDocumentAsync(url, cancellationToken).ConfigureAwait(false);
        JsonElement root = json.RootElement;
        if (!root.TryGetProperty("s", out JsonElement status)
            || !string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase)
            || !root.TryGetProperty("c", out JsonElement closes))
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.NotFound, $"Finnhub не вернул крипто-котировку для {originalTicker}.");
        }

        JsonElement[] closeItems = closes.EnumerateArray().ToArray();
        if (closeItems.Length == 0)
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.NotFound, $"Finnhub не вернул крипто-котировку для {originalTicker}.");
        }

        decimal current = ReadDecimal(closeItems[^1]);
        if (current <= 0m)
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.NotFound, $"Finnhub не вернул цену для {originalTicker}.");
        }

        decimal open = current;
        decimal high = current;
        decimal low = current;
        if (root.TryGetProperty("o", out JsonElement opens))
        {
            JsonElement[] openItems = opens.EnumerateArray().ToArray();
            if (openItems.Length > 0)
            {
                open = ReadDecimal(openItems[^1]);
            }
        }

        if (root.TryGetProperty("h", out JsonElement highs))
        {
            JsonElement[] highItems = highs.EnumerateArray().ToArray();
            if (highItems.Length > 0)
            {
                high = ReadDecimal(highItems[^1]);
            }
        }

        if (root.TryGetProperty("l", out JsonElement lows))
        {
            JsonElement[] lowItems = lows.EnumerateArray().ToArray();
            if (lowItems.Length > 0)
            {
                low = ReadDecimal(lowItems[^1]);
            }
        }

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        if (root.TryGetProperty("t", out JsonElement times))
        {
            JsonElement[] timeItems = times.EnumerateArray().ToArray();
            if (timeItems.Length > 0 && timeItems[^1].TryGetInt64(out long epoch))
            {
                timestamp = DateTimeOffset.FromUnixTimeSeconds(epoch);
            }
        }

        QuoteData data = new(
            originalTicker,
            current,
            NormalizeCurrency(currency),
            timestamp,
            "finnhub",
            new QuoteOhlc(open <= 0m ? current : open, high <= 0m ? current : high, low <= 0m ? current : low, current),
            null);

        return QuoteProviderResult.Success(data);
    }

    private async Task<JsonDocument> GetJsonDocumentAsync(string url, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Finnhub error: {(int)response.StatusCode}");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    internal static bool TryMapCryptoSymbol(string ticker, out string finnhubSymbol)
    {
        string normalized = ticker.Trim().ToUpperInvariant();
        if (normalized.Contains(':', StringComparison.Ordinal))
        {
            finnhubSymbol = normalized;
            return true;
        }

        finnhubSymbol = normalized switch
        {
            "BTC" or "BITCOIN" => "BINANCE:BTCUSDT",
            "ETH" or "ETHEREUM" => "BINANCE:ETHUSDT",
            "BNB" => "BINANCE:BNBUSDT",
            "SOL" or "SOLANA" => "BINANCE:SOLUSDT",
            "XRP" => "BINANCE:XRPUSDT",
            "ADA" or "CARDANO" => "BINANCE:ADAUSDT",
            "DOGE" or "DOGECOIN" => "BINANCE:DOGEUSDT",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(finnhubSymbol);
    }

    private static decimal TryGetDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return 0m;
        }

        return ReadDecimal(value);
    }

    private static decimal ReadDecimal(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed) => parsed,
            _ => 0m,
        };
    }

    private static string NormalizeCurrency(string currency)
    {
        return string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
    }
}
