using System.Globalization;
using System.Net;
using System.Text.Json;
using Proxima.Core.Application.Taxes;
using Proxima.Core.Application.Settings;

namespace Proxima.Infrastructure.Taxes;

public sealed class BelarusbankExchangeRateProvider(HttpClient httpClient) : IExchangeRateSource
{
    public CurrencyProviderKind Kind => CurrencyProviderKind.Belarusbank;

    private static readonly IReadOnlyDictionary<string, BelarusbankCurrencyDescriptor> CurrencyDescriptors =
        new Dictionary<string, BelarusbankCurrencyDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = new("USD_out", 1m),
            ["EUR"] = new("EUR_out", 1m),
            ["RUB"] = new("RUB_out", 100m),
            ["GBP"] = new("GBP_out", 1m),
            ["CAD"] = new("CAD_out", 1m),
            ["PLN"] = new("PLN_out", 1m),
            ["UAH"] = new("UAH_out", 100m),
            ["SEK"] = new("SEK_out", 10m),
            ["CHF"] = new("CHF_out", 10m),
            ["JPY"] = new("JPY_out", 100m),
            ["CNY"] = new("CNY_out", 10m),
            ["CZK"] = new("CZK_out", 100m),
            ["NOK"] = new("NOK_out", 10m),
        };

    private readonly HttpClient _httpClient = httpClient;

    public async Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        string from = Normalize(fromCurrency);
        string to = Normalize(toCurrency);
        if (from == to)
        {
            return ExchangeRateResult.Success(1m, "belarusbank", date);
        }

        if ((from != "BYN" && !CurrencyDescriptors.ContainsKey(from)) || (to != "BYN" && !CurrencyDescriptors.ContainsKey(to)))
        {
            return ExchangeRateResult.Failure($"Belarusbank does not publish rate for {from}->{to}.");
        }

        try
        {
            JsonElement? exchangePoint = await TryLoadExchangePointAsync(cancellationToken).ConfigureAwait(false);
            if (exchangePoint is null)
            {
                return ExchangeRateResult.Failure("Belarusbank response is empty.");
            }

            Dictionary<string, decimal> bynRates = new(StringComparer.OrdinalIgnoreCase)
            {
                ["BYN"] = 1m,
            };

            foreach ((string code, BelarusbankCurrencyDescriptor descriptor) in CurrencyDescriptors)
            {
                AddCurrencyRate(exchangePoint.Value, bynRates, code, descriptor);
            }

            if (!bynRates.TryGetValue(from, out decimal fromByn) || !bynRates.TryGetValue(to, out decimal toByn) || fromByn <= 0m || toByn <= 0m)
            {
                return ExchangeRateResult.Failure($"Currency pair {from}->{to} is not supported by Belarusbank adapter.");
            }

            decimal rate = fromByn / toByn;
            return ExchangeRateResult.Success(decimal.Round(rate, 8), "belarusbank", date);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return ExchangeRateResult.Failure($"Belarusbank request timed out: {ex.Message}");
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

    private async Task<JsonElement?> TryLoadExchangePointAsync(CancellationToken cancellationToken)
    {
        foreach (string url in BuildUrls())
        {
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
                {
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                JsonElement root = json.RootElement;
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                {
                    continue;
                }

                JsonElement selected = SelectBestExchangePoint(root);
                return selected.Clone();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildUrls()
    {
        yield return "https://belarusbank.by/api/kursExchange?city=%D0%9C%D0%B8%D0%BD%D1%81%D0%BA";
        yield return "https://belarusbank.by/api/kursExchange";
    }

    private static JsonElement SelectBestExchangePoint(JsonElement root)
    {
        foreach (JsonElement item in root.EnumerateArray())
        {
            if (HasAnyValidRate(item))
            {
                return item;
            }
        }

        return root[0];
    }

    private static bool HasAnyValidRate(JsonElement root)
    {
        foreach (BelarusbankCurrencyDescriptor descriptor in CurrencyDescriptors.Values)
        {
            if (TryReadScaledDecimal(root, descriptor.Key, descriptor.Scale, out decimal value) && value > 0m)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddCurrencyRate(JsonElement root, IDictionary<string, decimal> target, string code, BelarusbankCurrencyDescriptor descriptor)
    {
        if (TryReadScaledDecimal(root, descriptor.Key, descriptor.Scale, out decimal parsed) && parsed > 0m)
        {
            target[code] = parsed;
        }
    }

    private static bool TryReadScaledDecimal(JsonElement root, string key, decimal scale, out decimal parsed)
    {
        parsed = 0m;
        if (!root.TryGetProperty(key, out JsonElement value))
        {
            return false;
        }

        string? raw = value.ValueKind == JsonValueKind.Number
            ? value.GetRawText()
            : value.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string normalized = raw.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal rate) || rate <= 0m || scale <= 0m)
        {
            return false;
        }

        parsed = rate / scale;
        return true;
    }

    private static string Normalize(string currency) => string.IsNullOrWhiteSpace(currency)
        ? "BYN"
        : currency.Trim().ToUpperInvariant();

    private sealed record BelarusbankCurrencyDescriptor(string Key, decimal Scale);
}
