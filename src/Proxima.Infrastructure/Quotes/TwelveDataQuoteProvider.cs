using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Proxima.Core.Application.MarketData;
using Proxima.Core.Application.Quotes;
using Proxima.Infrastructure.MarketData;

namespace Proxima.Infrastructure.Quotes;

public sealed class TwelveDataQuoteProvider(HttpClient httpClient, string apiKey) : IQuoteProvider
{
    private static readonly Uri BaseUri = new("https://api.twelvedata.com/");

    private readonly HttpClient _httpClient = httpClient;
    private readonly string _apiKey = apiKey;

    public async Task<QuoteProviderResult> GetLatestQuoteAsync(
        string ticker,
        string currency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.Unknown, "Ticker is required.");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.Unauthorized, "Twelve Data API key не задан.");
        }

        string symbol = MarketSymbolNormalizer.NormalizeForTwelveData(ticker);
        try
        {
            string url = $"quote?symbol={Uri.EscapeDataString(symbol)}";
            using JsonDocument json = await GetJsonDocumentAsync(url, cancellationToken).ConfigureAwait(false);
            JsonElement root = json.RootElement;
            TwelveDataApiGuard.ThrowIfError(root);

            decimal current = FirstPositive(root, "close", "price");
            if (current <= 0m)
            {
                return QuoteProviderResult.Failure(QuoteProviderErrorKind.NotFound, $"Twelve Data не вернул цену для {symbol}.");
            }

            decimal open = FirstPositive(root, "open", "previous_close", "close");
            decimal high = FirstPositive(root, "high", "close");
            decimal low = FirstPositive(root, "low", "close");
            decimal volume = FirstPositive(root, "volume");
            string quoteCurrency = GetString(root, "currency");
            DateTimeOffset timestamp = ReadTimestamp(root);

            QuoteData data = new(
                symbol,
                current,
                NormalizeCurrency(string.IsNullOrWhiteSpace(quoteCurrency) ? currency : quoteCurrency),
                timestamp,
                "twelvedata",
                new QuoteOhlc(
                    open <= 0m ? current : open,
                    high <= 0m ? current : high,
                    low <= 0m ? current : low,
                    current),
                volume > 0m ? volume : null);

            return QuoteProviderResult.Success(data);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.Unauthorized, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.NotFound, ex.Message);
        }
        catch (Exception ex)
        {
            return QuoteProviderResult.Failure(QuoteProviderErrorKind.Network, $"Twelve Data request failed: {ex.Message}");
        }
    }

    private async Task<JsonDocument> GetJsonDocumentAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(BaseUri, relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("apikey", _apiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string message = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                    "Twelve Data API key недействителен или тариф не дает доступ к endpoint.",
                (System.Net.HttpStatusCode)429 =>
                    "Превышен лимит запросов Twelve Data. Повторите позже.",
                _ => $"Twelve Data вернул HTTP {(int)response.StatusCode}.",
            };

            throw new HttpRequestException(message);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset ReadTimestamp(JsonElement root)
    {
        if (root.TryGetProperty("timestamp", out JsonElement timestamp)
            && timestamp.TryGetInt64(out long epoch)
            && epoch > 0)
        {
            return DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        string datetime = GetString(root, "datetime");
        if (DateTimeOffset.TryParse(datetime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow;
    }

    private static decimal FirstPositive(JsonElement root, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            decimal value = TryGetDecimal(root, propertyName);
            if (value > 0m)
            {
                return value;
            }
        }

        return 0m;
    }

    private static decimal TryGetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
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
            JsonValueKind.String when decimal.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal parsed) => parsed,
            _ => 0m,
        };
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
    }

    private static string NormalizeCurrency(string currency)
    {
        return string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
    }
}
