using System.Globalization;
using System.Text.Json;
using Proxima.Application.AssetDetails;
using Proxima.Application.Auth;
using Proxima.Application.Settings;
using Proxima.Infrastructure.Quotes;

namespace Proxima.Infrastructure.AssetDetails;

public sealed class FinnhubAssetMarketDataProvider(
    ICurrentUserContext currentUser,
    ISettingsService settingsService,
    HttpClient httpClient)
{
    public async Task<FinnhubAssetMarketData?> TryLoadAsync(
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

        if (settings is null || settings.QuoteProvider != QuoteProviderKind.Finnhub)
        {
            return null;
        }

        string apiKey = ConfigurableQuoteProvider.UnprotectApiKey(settings.FinnhubApiKeyProtected);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            string normalizedTicker = ticker.Trim().ToUpperInvariant();
            bool isCrypto = FinnhubQuoteProvider.TryMapCryptoSymbol(normalizedTicker, out string cryptoSymbol);
            string finnhubSymbol = isCrypto ? cryptoSymbol : normalizedTicker;
            string safeTicker = Uri.EscapeDataString(finnhubSymbol);

            string? name = isCrypto ? normalizedTicker : null;
            string? currency = null;
            decimal? marketCapUsd = null;
            decimal? fdvUsd = null;
            decimal? peRatio = null;
            decimal? beta = null;
            decimal? volumeUnits = null;
            decimal? volumeUsd = null;
            decimal? shareOutstanding = null;

            if (!isCrypto)
            {
                using JsonDocument? profile = await GetJsonAsync(
                    $"https://finnhub.io/api/v1/stock/profile2?symbol={safeTicker}&token={Uri.EscapeDataString(apiKey)}",
                    cancellationToken).ConfigureAwait(false);

                if (profile is not null)
                {
                    JsonElement root = profile.RootElement;

                    name = TryGetString(root, "name");
                    currency = TryGetString(root, "currency");

                    decimal marketCapMillions = TryGetDecimal(root, "marketCapitalization");
                    if (marketCapMillions > 0m)
                    {
                        marketCapUsd = marketCapMillions * 1_000_000m;
                    }

                    decimal sharesMillions = TryGetDecimal(root, "shareOutstanding");
                    if (sharesMillions > 0m)
                    {
                        shareOutstanding = sharesMillions * 1_000_000m;
                    }
                }

                using JsonDocument? metrics = await GetJsonAsync(
                    $"https://finnhub.io/api/v1/stock/metric?symbol={safeTicker}&metric=all&token={Uri.EscapeDataString(apiKey)}",
                    cancellationToken).ConfigureAwait(false);

                if (metrics is not null && metrics.RootElement.TryGetProperty("metric", out JsonElement metric))
                {
                    decimal metricMarketCapMillions = TryGetDecimal(metric, "marketCapitalization");
                    if (metricMarketCapMillions > 0m)
                    {
                        marketCapUsd = metricMarketCapMillions * 1_000_000m;
                    }

                    peRatio = FirstPositive(metric, "peNormalizedAnnual", "peBasicExclExtraTTM", "peTTM");
                    beta = FirstPositive(metric, "beta");
                    volumeUnits = FirstPositive(metric, "10DayAverageTradingVolume", "3MonthAverageTradingVolume");
                }
            }

            IReadOnlyList<AssetDetailsCandle> candles = await TryLoadCandlesAsync(
                safeTicker,
                timeframe,
                apiKey,
                isCrypto,
                cancellationToken).ConfigureAwait(false);

            if (candles.Count > 0 && volumeUnits.HasValue)
            {
                volumeUsd = volumeUnits.Value * candles[^1].Close;
            }

            if (marketCapUsd.HasValue)
            {
                fdvUsd = marketCapUsd.Value * 1.02m;
            }

            FinnhubAssetMarketData data = new(
                name,
                currency,
                marketCapUsd,
                fdvUsd,
                peRatio,
                beta,
                volumeUnits,
                volumeUsd,
                shareOutstanding,
                candles);

            return data.HasAnyData ? data : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<AssetDetailsCandle>> TryLoadCandlesAsync(
        string safeTicker,
        string timeframe,
        string apiKey,
        bool isCrypto,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        (string resolution, DateTimeOffset from) = timeframe switch
        {
            "1ч" => ("1", now.AddHours(-1)),
            "1д" => ("15", now.AddDays(-1)),
            "7д" => ("60", now.AddDays(-7)),
            "30д" => ("D", now.AddDays(-30)),
            "1г" => ("W", now.AddYears(-1)),
            _ => ("15", now.AddDays(-1))
        };

        string endpoint = isCrypto ? "crypto/candle" : "stock/candle";
        string url =
            $"https://finnhub.io/api/v1/{endpoint}?symbol={safeTicker}&resolution={resolution}&from={from.ToUnixTimeSeconds()}&to={now.ToUnixTimeSeconds()}&token={Uri.EscapeDataString(apiKey)}";

        using JsonDocument? json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);

        if (json is null)
        {
            return [];
        }

        if (!json.RootElement.TryGetProperty("s", out JsonElement status)
            || !string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (!json.RootElement.TryGetProperty("t", out JsonElement times)
            || !json.RootElement.TryGetProperty("o", out JsonElement opens)
            || !json.RootElement.TryGetProperty("h", out JsonElement highs)
            || !json.RootElement.TryGetProperty("l", out JsonElement lows)
            || !json.RootElement.TryGetProperty("c", out JsonElement closes))
        {
            return [];
        }

        JsonElement[] timeItems = times.EnumerateArray().ToArray();
        JsonElement[] openItems = opens.EnumerateArray().ToArray();
        JsonElement[] highItems = highs.EnumerateArray().ToArray();
        JsonElement[] lowItems = lows.EnumerateArray().ToArray();
        JsonElement[] closeItems = closes.EnumerateArray().ToArray();
        JsonElement[] volumeItems = json.RootElement.TryGetProperty("v", out JsonElement volumeElement)
            ? volumeElement.EnumerateArray().ToArray()
            : [];

        int count = new[]
        {
            timeItems.Length,
            openItems.Length,
            highItems.Length,
            lowItems.Length,
            closeItems.Length
        }.Min();

        List<AssetDetailsCandle> result = new(count);

        for (int i = 0; i < count; i++)
        {
            decimal volume = i < volumeItems.Length
                ? ReadDecimal(volumeItems[i])
                : 0m;

            long epoch = timeItems[i].GetInt64();

            AssetDetailsCandle candle = new(
                DateTimeOffset.FromUnixTimeSeconds(epoch),
                ReadDecimal(openItems[i]),
                ReadDecimal(highItems[i]),
                ReadDecimal(lowItems[i]),
                ReadDecimal(closeItems[i]),
                volume);

            if (candle.Close > 0m)
            {
                result.Add(candle);
            }
        }

        return result;
    }

    private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        return await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.GetString();
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
            _ => 0m
        };
    }

    private static decimal? FirstPositive(JsonElement root, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            decimal value = TryGetDecimal(root, propertyName);
            if (value > 0m)
            {
                return value;
            }
        }

        return null;
    }
}

public sealed record FinnhubAssetMarketData(
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
