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
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.Unauthorized, "Finnhub API key is not configured.");
        }

        string safeTicker = Uri.EscapeDataString(ticker.Trim().ToUpperInvariant());
        string url = $"https://finnhub.io/api/v1/quote?symbol={safeTicker}&token={Uri.EscapeDataString(_apiKey)}";

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return QuoteProviderResult.Failure(QuoteProviderErrorKind.Network, $"Finnhub error: {(int)response.StatusCode}");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement root = json.RootElement;
            decimal current = TryGetDecimal(root, "c");
            if (current <= 0m)
            {
                return QuoteProviderResult.Failure(QuoteProviderErrorKind.NotFound, $"No quote for {ticker}.");
            }

            decimal open = TryGetDecimal(root, "o");
            decimal high = TryGetDecimal(root, "h");
            decimal low = TryGetDecimal(root, "l");
            long unix = root.TryGetProperty("t", out JsonElement tEl) && tEl.TryGetInt64(out long epoch)
                ? epoch
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            DateTimeOffset ts = DateTimeOffset.FromUnixTimeSeconds(unix);

            QuoteOhlc ohlc = new(open <= 0 ? current : open, high <= 0 ? current : high, low <= 0 ? current : low, current);
            QuoteData data = new(
                ticker.Trim().ToUpperInvariant(),
                current,
                string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
                ts,
                "finnhub",
                ohlc,
                null);

            return QuoteProviderResult.Success(data);
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

    private static decimal TryGetDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return 0m;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed) => parsed,
            _ => 0m,
        };
    }
}
