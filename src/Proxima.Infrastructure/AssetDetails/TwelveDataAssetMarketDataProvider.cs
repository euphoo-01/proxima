using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Proxima.Application.AssetDetails;
using Proxima.Application.Auth;
using Proxima.Application.MarketData;
using Proxima.Application.Settings;
using Proxima.Infrastructure.MarketData;
using Proxima.Infrastructure.Quotes;

namespace Proxima.Infrastructure.AssetDetails;

public sealed class TwelveDataAssetMarketDataProvider(
    ICurrentUserContext currentUser,
    ISettingsService settingsService,
    HttpClient httpClient)
{
    private static readonly Uri BaseUri = new("https://api.twelvedata.com/");

    public async Task<TwelveDataAssetMarketData?> TryLoadAsync(
        string ticker,
        string timeframe,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return null;
        }

        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
        {
            return null;
        }

        UserSettings? settings = await settingsService
            .GetAsync(currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null || settings.QuoteProvider != QuoteProviderKind.TwelveData)
        {
            return null;
        }

        string apiKey = ConfigurableQuoteProvider.UnprotectApiKey(settings.TwelveDataApiKeyProtected);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            string symbol = MarketSymbolNormalizer.NormalizeForTwelveData(ticker);
            TwelveDataQuoteSnapshot? quote = await TryLoadQuoteAsync(symbol, apiKey, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<AssetDetailsCandle> candles = await LoadCandlesAsync(symbol, timeframe, apiKey, cancellationToken).ConfigureAwait(false);

            decimal? volumeUnits = quote?.Volume > 0m
                ? quote.Volume
                : candles.Count > 0 && candles[^1].Volume > 0m
                    ? candles[^1].Volume
                    : null;

            decimal? volumeUsd = volumeUnits.HasValue && candles.Count > 0
                ? volumeUnits.Value * candles[^1].Close
                : quote?.Close is > 0m && volumeUnits.HasValue
                    ? volumeUnits.Value * quote.Close
                    : null;

            TwelveDataAssetMarketData data = new(
                quote?.Name,
                quote?.Currency,
                null,
                null,
                null,
                null,
                volumeUnits,
                volumeUsd,
                null,
                candles);

            return data.HasAnyData ? data : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<TwelveDataQuoteSnapshot?> TryLoadQuoteAsync(
        string symbol,
        string apiKey,
        CancellationToken cancellationToken)
    {
        string url = $"quote?symbol={Uri.EscapeDataString(symbol)}";
        using JsonDocument json = await ReadJsonAsync(url, apiKey, cancellationToken).ConfigureAwait(false);
        JsonElement root = json.RootElement;
        TwelveDataApiGuard.ThrowIfError(root);

        decimal close = FirstPositive(root, "close", "price");
        return new TwelveDataQuoteSnapshot(
            FirstNonEmpty(GetString(root, "name"), GetString(root, "symbol"), symbol),
            FirstNonEmpty(GetString(root, "currency"), InferCurrency(symbol)),
            close,
            FirstPositive(root, "volume"));
    }

    private async Task<IReadOnlyList<AssetDetailsCandle>> LoadCandlesAsync(
        string symbol,
        string timeframe,
        string apiKey,
        CancellationToken cancellationToken)
    {
        (string interval, int outputSize) = timeframe switch
        {
            "1ч" => ("1min", 60),
            "1д" => ("15min", 96),
            "7д" => ("1h", 168),
            "30д" => ("1day", 30),
            "1г" => ("1day", 365),
            _ => ("15min", 96),
        };

        string url = $"time_series?symbol={Uri.EscapeDataString(symbol)}&interval={Uri.EscapeDataString(interval)}&outputsize={outputSize}&order=ASC";
        using JsonDocument json = await ReadJsonAsync(url, apiKey, cancellationToken).ConfigureAwait(false);
        JsonElement root = json.RootElement;
        TwelveDataApiGuard.ThrowIfError(root);

        if (!root.TryGetProperty("values", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<AssetDetailsCandle> result = [];
        foreach (JsonElement item in values.EnumerateArray())
        {
            decimal open = TryGetDecimal(item, "open");
            decimal high = TryGetDecimal(item, "high");
            decimal low = TryGetDecimal(item, "low");
            decimal close = TryGetDecimal(item, "close");
            decimal volume = TryGetDecimal(item, "volume");
            DateTimeOffset timestamp = ParseDateTime(GetString(item, "datetime"));

            if (close <= 0m)
            {
                continue;
            }

            result.Add(new AssetDetailsCandle(
                timestamp,
                open <= 0m ? close : open,
                high <= 0m ? close : high,
                low <= 0m ? close : low,
                close,
                volume));
        }

        return result
            .OrderBy(item => item.Timestamp)
            .ToArray();
    }

    private async Task<JsonDocument> ReadJsonAsync(string relativeUrl, string apiKey, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(BaseUri, relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("apikey", apiKey);

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string message = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                    "Twelve Data отклонил запрос. Проверьте API key и тариф.",
                (System.Net.HttpStatusCode)429 =>
                    "Превышен лимит запросов Twelve Data. Повторите позже.",
                _ => $"Twelve Data вернул HTTP {(int)response.StatusCode}.",
            };

            throw new InvalidOperationException(message);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset ParseDateTime(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
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

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string InferCurrency(string symbol)
    {
        int slash = symbol.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 && slash + 1 < symbol.Length ? symbol[(slash + 1)..] : "USD";
    }

    private sealed record TwelveDataQuoteSnapshot(string Name, string Currency, decimal Close, decimal Volume);
}

public sealed record TwelveDataAssetMarketData(
    string? Name,
    string? Currency,
    decimal? MarketCapUsd,
    decimal? FdvUsd,
    decimal? PeRatio,
    decimal? Beta,
    decimal? VolumeUnits,
    decimal? VolumeUsd,
    decimal? ShareOutstanding,
    IReadOnlyList<AssetDetailsCandle> Candles)
{
    public bool HasAnyData =>
        !string.IsNullOrWhiteSpace(Name)
        || MarketCapUsd.HasValue
        || FdvUsd.HasValue
        || PeRatio.HasValue
        || Beta.HasValue
        || VolumeUnits.HasValue
        || Candles.Count > 0;
}
