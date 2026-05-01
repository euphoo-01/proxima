using System.Globalization;
using System.Text.Json;
using Proxima.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public sealed class BelarusbankExchangeRateProvider(HttpClient httpClient) : IExchangeRateProvider
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        string from = fromCurrency.Trim().ToUpperInvariant();
        string to = toCurrency.Trim().ToUpperInvariant();
        if (from == to)
        {
            return ExchangeRateResult.Success(1m, "belarusbank", date);
        }

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync("https://belarusbank.by/api/kursExchange?city=Минск", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ExchangeRateResult.Failure($"Belarusbank error: {(int)response.StatusCode}");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                return ExchangeRateResult.Failure("Belarusbank response is empty.");
            }

            JsonElement first = root[0];
            Dictionary<string, decimal> bynRates = new(StringComparer.OrdinalIgnoreCase)
            {
                ["BYN"] = 1m,
            };

            AddCurrencyRate(first, bynRates, "USD", "USD_out");
            AddCurrencyRate(first, bynRates, "EUR", "EUR_out");
            AddCurrencyRate(first, bynRates, "RUB", "RUB_out");

            if (!bynRates.TryGetValue(from, out decimal fromByn) || !bynRates.TryGetValue(to, out decimal toByn) || fromByn <= 0m || toByn <= 0m)
            {
                return ExchangeRateResult.Failure($"Currency pair {from}->{to} is not supported by Belarusbank adapter.");
            }

            decimal rate = fromByn / toByn;
            return ExchangeRateResult.Success(rate, "belarusbank", date);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ExchangeRateResult.Failure($"Belarusbank request failed: {ex.Message}");
        }
    }

    private static void AddCurrencyRate(JsonElement root, IDictionary<string, decimal> target, string code, string key)
    {
        if (!root.TryGetProperty(key, out JsonElement value))
        {
            return;
        }

        string? raw = value.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        string normalized = raw.Replace(',', '.');
        if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed) && parsed > 0m)
        {
            target[code] = parsed;
        }
    }
}
