using System.Text.RegularExpressions;

namespace Proxima.Application.MarketData;

public static class MarketSymbolNormalizer
{
    public static string NormalizeForTwelveData(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim().ToUpperInvariant();
        normalized = normalized.Replace("-", "/", StringComparison.Ordinal);
        normalized = Regex.Replace(normalized, "\\s+", string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

        if (normalized.Contains('/', StringComparison.Ordinal))
        {
            return normalized;
        }

        return normalized switch
        {
            "BTC" or "BITCOIN" or "BTCUSDT" or "BTCUSD" => "BTC/USD",
            "ETH" or "ETHEREUM" or "ETHUSDT" or "ETHUSD" => "ETH/USD",
            "BNB" or "BNBUSDT" or "BNBUSD" => "BNB/USD",
            "SOL" or "SOLANA" or "SOLUSDT" or "SOLUSD" => "SOL/USD",
            "XRP" or "XRPUSDT" or "XRPUSD" => "XRP/USD",
            "ADA" or "CARDANO" or "ADAUSDT" or "ADAUSD" => "ADA/USD",
            "DOGE" or "DOGECOIN" or "DOGEUSDT" or "DOGEUSD" => "DOGE/USD",
            "DOT" or "POLKADOT" or "DOTUSDT" or "DOTUSD" => "DOT/USD",
            "AVAX" or "AVALANCHE" or "AVAXUSDT" or "AVAXUSD" => "AVAX/USD",
            "MATIC" or "POLYGON" or "MATICUSDT" or "MATICUSD" => "MATIC/USD",
            _ when normalized.StartsWith("BINANCE:", StringComparison.Ordinal) => NormalizeExchangeQualifiedCrypto(normalized[8..]),
            _ when normalized.StartsWith("COINBASE:", StringComparison.Ordinal) => NormalizeExchangeQualifiedCrypto(normalized[9..]),
            _ when Regex.IsMatch(normalized, "^[A-Z0-9]{2,10}USDT$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)) =>
                $"{normalized[..^4]}/USD",
            _ when Regex.IsMatch(normalized, "^[A-Z0-9]{2,10}USD$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)) =>
                $"{normalized[..^3]}/USD",
            _ => normalized,
        };
    }

    private static string NormalizeExchangeQualifiedCrypto(string value)
    {
        string compact = value.Replace("/", string.Empty, StringComparison.Ordinal);
        if (compact.EndsWith("USDT", StringComparison.Ordinal))
        {
            return $"{compact[..^4]}/USD";
        }

        if (compact.EndsWith("USD", StringComparison.Ordinal))
        {
            return $"{compact[..^3]}/USD";
        }

        return compact;
    }
}
