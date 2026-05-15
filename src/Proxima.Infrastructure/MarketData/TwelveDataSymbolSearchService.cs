using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using Proxima.Core.Application.Auth;
using Proxima.Core.Application.MarketData;
using Proxima.Core.Application.Settings;
using Proxima.Core.Domain.Assets;
using Proxima.Infrastructure.Quotes;

namespace Proxima.Infrastructure.MarketData;

public sealed class TwelveDataSymbolSearchService(
    ICurrentUserContext currentUser,
    ISettingsService settingsService,
    HttpClient httpClient) : IMarketSymbolSearchService
{
    private static readonly Uri BaseUri = new("https://api.twelvedata.com/");
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, CachedSearchResult> SearchCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly MarketSymbolCandidate[] LocalSymbols =
    [
        Cash("USD", "US Dollar"),
        Cash("EUR", "Euro"),
        Cash("BYN", "Belarusian Ruble"),
        Cash("RUB", "Russian Ruble"),
        Cash("GBP", "British Pound"),
        Cash("CHF", "Swiss Franc"),
        Cash("PLN", "Polish Zloty"),
        Cash("CNY", "Chinese Yuan"),
        Cash("JPY", "Japanese Yen"),
        Cash("CAD", "Canadian Dollar"),
        Cash("AUD", "Australian Dollar"),
        Crypto("BTC/USD", "Bitcoin US Dollar"),
        Crypto("ETH/USD", "Ethereum US Dollar"),
        Crypto("BNB/USD", "BNB US Dollar"),
        Crypto("SOL/USD", "Solana US Dollar"),
        Crypto("XRP/USD", "XRP US Dollar"),
        Crypto("ADA/USD", "Cardano US Dollar"),
        Crypto("DOGE/USD", "Dogecoin US Dollar"),
        Crypto("DOT/USD", "Polkadot US Dollar"),
        Crypto("AVAX/USD", "Avalanche US Dollar"),
        Crypto("MATIC/USD", "Polygon US Dollar"),
        Forex("EUR/USD", "Euro US Dollar"),
        Forex("GBP/USD", "British Pound US Dollar"),
        Forex("USD/JPY", "US Dollar Japanese Yen"),
        Forex("USD/CHF", "US Dollar Swiss Franc"),
        Forex("USD/CAD", "US Dollar Canadian Dollar"),
        Forex("AUD/USD", "Australian Dollar US Dollar"),
    ];

    public async Task<MarketSymbolSearchResult> SearchAsync(
        string query,
        int limit = 12,
        CancellationToken cancellationToken = default)
    {
        string normalizedQuery = NormalizeQuery(query);
        if (normalizedQuery.Length == 0)
        {
            return MarketSymbolSearchResult.Success(Array.Empty<MarketSymbolCandidate>());
        }

        int normalizedLimit = Math.Clamp(limit, 1, 50);
        MarketSymbolCandidate[] localMatches = FindLocalSymbols(normalizedQuery, normalizedLimit);

        // Local cash/major crypto symbols must remain available even before an API key is configured.
        // This keeps manual cash input usable and avoids treating USD/EUR/BYN as stock tickers.
        if (IsExactLocalOnlyQuery(normalizedQuery, localMatches))
        {
            return MarketSymbolSearchResult.Success(localMatches.Take(normalizedLimit).ToArray());
        }

        string apiKey = await ResolveApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return localMatches.Length > 0
                ? MarketSymbolSearchResult.Success(localMatches.Take(normalizedLimit).ToArray())
                : MarketSymbolSearchResult.Failure("Twelve Data API key не задан. Сохраните ключ в профиле → API Ключи.");
        }

        string cacheKey = $"{normalizedQuery}|{normalizedLimit}";
        if (SearchCache.TryGetValue(cacheKey, out CachedSearchResult? cached)
            && DateTimeOffset.UtcNow - cached.CreatedAt <= SearchCacheTtl)
        {
            return MarketSymbolSearchResult.Success(cached.Symbols);
        }

        try
        {
            string url = $"symbol_search?symbol={Uri.EscapeDataString(normalizedQuery)}";
            using JsonDocument json = await ReadJsonAsync(url, apiKey, cancellationToken).ConfigureAwait(false);
            JsonElement root = json.RootElement;
            TwelveDataApiGuard.ThrowIfError(root);

            IEnumerable<MarketSymbolCandidate> remoteSymbols = Enumerable.Empty<MarketSymbolCandidate>();
            if (root.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array)
            {
                remoteSymbols = data
                    .EnumerateArray()
                    .Select(ToCandidate)
                    .Where(item => !string.IsNullOrWhiteSpace(item.Symbol));
            }

            MarketSymbolCandidate[] symbols = localMatches
                .Concat(remoteSymbols)
                .GroupBy(item => KeyFor(item), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => Rank(item, normalizedQuery))
                .ThenBy(item => item.DisplaySymbol, StringComparer.OrdinalIgnoreCase)
                .Take(normalizedLimit)
                .ToArray();

            SearchCache[cacheKey] = new CachedSearchResult(DateTimeOffset.UtcNow, symbols);
            return MarketSymbolSearchResult.Success(symbols);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            return localMatches.Length > 0
                ? MarketSymbolSearchResult.Success(localMatches.Take(normalizedLimit).ToArray())
                : MarketSymbolSearchResult.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return localMatches.Length > 0
                ? MarketSymbolSearchResult.Success(localMatches.Take(normalizedLimit).ToArray())
                : MarketSymbolSearchResult.Failure($"Не удалось получить список инструментов Twelve Data: {ex.Message}");
        }
    }

    public async Task<MarketSymbolSearchResult> ResolveAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        string normalizedQuery = NormalizeQuery(query);
        if (normalizedQuery.Length == 0)
        {
            return MarketSymbolSearchResult.Failure("Введите тикер или название инструмента.");
        }

        MarketSymbolCandidate[] localExact = FindLocalSymbols(normalizedQuery, 20)
            .Where(item => IsExactMatch(item, normalizedQuery))
            .ToArray();
        if (localExact.Length > 0)
        {
            return MarketSymbolSearchResult.Success([localExact[0]]);
        }

        string normalizedSymbol = MarketSymbolNormalizer.NormalizeForTwelveData(normalizedQuery);
        MarketSymbolSearchResult result = await SearchAsync(normalizedQuery, 20, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return result;
        }

        MarketSymbolCandidate? exact = result.Symbols.FirstOrDefault(item =>
            string.Equals(item.Symbol, normalizedSymbol, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.DisplaySymbol, normalizedSymbol, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Compact(item.Symbol), Compact(normalizedSymbol), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Compact(item.DisplaySymbol), Compact(normalizedSymbol), StringComparison.OrdinalIgnoreCase));

        exact ??= result.Symbols.FirstOrDefault(item =>
            item.Symbol.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase)
            || item.DisplaySymbol.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase));

        return exact is null
            ? MarketSymbolSearchResult.Failure("Twelve Data не нашёл такой тикер. Выберите инструмент из подсказок.")
            : MarketSymbolSearchResult.Success([exact]);
    }

    private async Task<string> ResolveApiKeyAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
        {
            return string.Empty;
        }

        UserSettings? settings = await settingsService
            .GetAsync(currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null || settings.QuoteProvider != QuoteProviderKind.TwelveData)
        {
            return string.Empty;
        }

        return ConfigurableQuoteProvider.UnprotectApiKey(settings.TwelveDataApiKeyProtected);
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
                    "Twelve Data API key недействителен или тариф не дает доступ к endpoint.",
                (System.Net.HttpStatusCode)429 =>
                    "Превышен лимит запросов Twelve Data. Повторите позже.",
                _ => $"Twelve Data вернул HTTP {(int)response.StatusCode}.",
            };

            throw new InvalidOperationException(message);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static MarketSymbolCandidate ToCandidate(JsonElement item)
    {
        string symbol = MarketSymbolNormalizer.NormalizeForTwelveData(GetString(item, "symbol"));
        string name = FirstNonEmpty(
            GetString(item, "instrument_name"),
            GetString(item, "name"),
            symbol);
        string type = FirstNonEmpty(
            GetString(item, "instrument_type"),
            GetString(item, "type"),
            "Instrument");
        string exchange = GetString(item, "exchange");
        string mic = FirstNonEmpty(GetString(item, "mic_code"), GetString(item, "mic"));
        string currency = FirstNonEmpty(GetString(item, "currency"), InferCurrency(symbol));

        return new MarketSymbolCandidate(
            symbol,
            symbol,
            name,
            type,
            currency,
            exchange,
            string.IsNullOrWhiteSpace(mic) ? null : mic,
            InferAssetType(type, symbol));
    }

    private static MarketSymbolCandidate[] FindLocalSymbols(string query, int limit)
    {
        string compactQuery = Compact(query);
        bool asksForCash = IsCashQuery(query);

        return LocalSymbols
            .Where(item => asksForCash && item.AssetType is AssetType.Cash or AssetType.Currency || LocalSymbolMatches(item, query, compactQuery))
            .OrderBy(item => Rank(item, query))
            .ThenBy(item => item.DisplaySymbol, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static bool LocalSymbolMatches(MarketSymbolCandidate item, string query, string compactQuery)
    {
        return item.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Compact(item.Symbol).Contains(compactQuery, StringComparison.OrdinalIgnoreCase)
            || Compact(item.DisplaySymbol).Contains(compactQuery, StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExactLocalOnlyQuery(string query, IReadOnlyList<MarketSymbolCandidate> localMatches)
    {
        if (localMatches.Count == 0)
        {
            return false;
        }

        return localMatches.Any(item => IsExactMatch(item, query) && item.AssetType is AssetType.Cash or AssetType.Currency);
    }

    private static bool IsExactMatch(MarketSymbolCandidate item, string query)
    {
        string compactQuery = Compact(query);
        return string.Equals(item.Symbol, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.DisplaySymbol, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Compact(item.Symbol), compactQuery, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Compact(item.DisplaySymbol), compactQuery, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Description, query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCashQuery(string query)
    {
        string value = query.Trim().ToUpperInvariant();
        return value is "CASH" or "FIAT" or "MONEY" or "НАЛ" or "НАЛИЧНОСТЬ" or "ВАЛЮТА" or "ДЕНЬГИ";
    }

    private static MarketSymbolCandidate Cash(string code, string name)
    {
        return new MarketSymbolCandidate(code, code, name, "Cash", code, "Cash", null, AssetType.Cash);
    }

    private static MarketSymbolCandidate Crypto(string symbol, string name)
    {
        return new MarketSymbolCandidate(symbol, symbol, name, "Cryptocurrency", InferCurrency(symbol), "Twelve Data", null, AssetType.Crypto);
    }

    private static MarketSymbolCandidate Forex(string symbol, string name)
    {
        return new MarketSymbolCandidate(symbol, symbol, name, "Forex Pair", InferCurrency(symbol), "Forex", null, AssetType.Currency);
    }

    private static AssetType InferAssetType(string type, string symbol)
    {
        string source = $"{type} {symbol}".ToLowerInvariant();
        if (source.Contains("crypto", StringComparison.Ordinal) || symbol.Contains('/', StringComparison.Ordinal) && IsCryptoBase(symbol.Split('/')[0]))
        {
            return AssetType.Crypto;
        }

        if (source.Contains("forex", StringComparison.Ordinal) || source.Contains("physical currency", StringComparison.Ordinal))
        {
            return AssetType.Currency;
        }

        if (source.Contains("cash", StringComparison.Ordinal))
        {
            return AssetType.Cash;
        }

        if (source.Contains("etf", StringComparison.Ordinal) || source.Contains("fund", StringComparison.Ordinal))
        {
            return AssetType.Etf;
        }

        if (source.Contains("bond", StringComparison.Ordinal))
        {
            return AssetType.Bond;
        }

        return AssetType.Stock;
    }

    private static bool IsCryptoBase(string value)
    {
        return value is "BTC" or "ETH" or "BNB" or "SOL" or "XRP" or "ADA" or "DOGE" or "DOT" or "AVAX" or "MATIC";
    }

    private static string InferCurrency(string symbol)
    {
        int slash = symbol.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 && slash + 1 < symbol.Length ? symbol[(slash + 1)..] : "USD";
    }

    private static string KeyFor(MarketSymbolCandidate item)
    {
        return $"{item.Symbol}|{item.Exchange}|{item.Currency}|{item.AssetType}";
    }

    private static int Rank(MarketSymbolCandidate item, string query)
    {
        string compactSymbol = Compact(item.Symbol);
        string compactDisplay = Compact(item.DisplaySymbol);
        string compactQuery = Compact(query);

        if (string.Equals(item.Symbol, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(compactSymbol, compactQuery, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.DisplaySymbol, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(compactDisplay, compactQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (item.Symbol.StartsWith(query, StringComparison.OrdinalIgnoreCase)
            || compactSymbol.StartsWith(compactQuery, StringComparison.OrdinalIgnoreCase)
            || item.DisplaySymbol.StartsWith(query, StringComparison.OrdinalIgnoreCase)
            || compactDisplay.StartsWith(compactQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (item.Description.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private static string NormalizeQuery(string query)
    {
        return string.IsNullOrWhiteSpace(query)
            ? string.Empty
            : query.Trim().ToUpperInvariant();
    }

    private static string Compact(string value)
    {
        return value.Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
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

    private sealed record CachedSearchResult(DateTimeOffset CreatedAt, MarketSymbolCandidate[] Symbols);
}
