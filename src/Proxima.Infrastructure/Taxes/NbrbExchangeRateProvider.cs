using System.Globalization;
using System.Net;
using System.Text.Json;
using Proxima.Application.Taxes;

namespace Proxima.Infrastructure.Taxes;

public sealed class NbrbExchangeRateProvider(HttpClient httpClient) : IExchangeRateProvider
{
    private const int MaxPreviousDaysToProbe = 10;

    private static readonly IReadOnlySet<string> SupportedCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "USD",
        "EUR",
        "RUB",
        "GBP",
        "CHF",
        "CNY",
        "PLN",
        "UAH",
        "KZT",
        "CAD",
        "JPY",
        "SEK",
        "CZK",
        "NOK",
    };

    private readonly HttpClient _httpClient = httpClient;

    public async Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken = default)
    {
        string from = Normalize(fromCurrency);
        string to = Normalize(toCurrency);
        if (from == to)
        {
            return ExchangeRateResult.Success(1m, "nbrb", date);
        }

        if ((from != "BYN" && !SupportedCurrencies.Contains(from)) || (to != "BYN" && !SupportedCurrencies.Contains(to)))
        {
            return ExchangeRateResult.Failure($"NBRB does not publish rate for {from}->{to}.");
        }

        try
        {
            RateValue fromByn = from == "BYN" ? new RateValue(1m, date) : await GetBynPerUnitAsync(from, date, cancellationToken).ConfigureAwait(false);
            RateValue toByn = to == "BYN" ? new RateValue(1m, date) : await GetBynPerUnitAsync(to, date, cancellationToken).ConfigureAwait(false);

            if (fromByn.Value <= 0m || toByn.Value <= 0m)
            {
                return ExchangeRateResult.Failure($"NBRB returned invalid rate for {from}->{to}.");
            }

            DateOnly effectiveDate = from == "BYN" ? toByn.Date : fromByn.Date;
            return ExchangeRateResult.Success(decimal.Round(fromByn.Value / toByn.Value, 8), "nbrb", effectiveDate);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return ExchangeRateResult.Failure($"NBRB request timed out: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ExchangeRateResult.Failure($"NBRB request failed: {ex.Message}");
        }
    }

    private async Task<RateValue> GetBynPerUnitAsync(string currency, DateOnly date, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (int offset = 0; offset <= MaxPreviousDaysToProbe; offset++)
        {
            DateOnly requestedDate = date.AddDays(-offset);

            foreach (string baseUrl in BuildOfficialUrls(currency, requestedDate))
            {
                try
                {
                    RateValue? rate = await TryReadRateAsync(baseUrl, requestedDate, cancellationToken).ConfigureAwait(false);
                    if (rate is not null)
                    {
                        return rate.Value;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }
        }

        string message = lastError is null
            ? $"NBRB returned no official rate for {currency} around {date:yyyy-MM-dd}."
            : $"NBRB returned no official rate for {currency} around {date:yyyy-MM-dd}: {lastError.Message}";
        throw new InvalidOperationException(message);
    }

    private static IEnumerable<string> BuildOfficialUrls(string currency, DateOnly date)
    {
        string code = Uri.EscapeDataString(currency.ToUpperInvariant());
        string onDate = Uri.EscapeDataString(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        yield return $"https://api.nbrb.by/exrates/rates/{code}?parammode=2&onDate={onDate}";
        yield return $"https://www.nbrb.by/api/exrates/rates/{code}?parammode=2&onDate={onDate}";
    }

    private async Task<RateValue?> TryReadRateAsync(string url, DateOnly requestedDate, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode} for {url}.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement root = json.RootElement;

        decimal officialRate = ReadDecimal(root, "Cur_OfficialRate");
        decimal scale = ReadDecimal(root, "Cur_Scale");
        if (officialRate <= 0m || scale <= 0m)
        {
            return null;
        }

        DateOnly effectiveDate = ReadDate(root, "Date") ?? requestedDate;
        return new RateValue(officialRate / scale, effectiveDate);
    }

    private static decimal ReadDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
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

    private static DateOnly? ReadDate(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        string? raw = value.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime parsed)
            ? DateOnly.FromDateTime(parsed)
            : null;
    }

    private static string Normalize(string currency) => string.IsNullOrWhiteSpace(currency)
        ? "BYN"
        : currency.Trim().ToUpperInvariant();

    private readonly record struct RateValue(decimal Value, DateOnly Date);
}
