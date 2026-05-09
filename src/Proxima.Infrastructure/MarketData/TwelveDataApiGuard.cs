using System.Text.Json;

namespace Proxima.Infrastructure.MarketData;

internal static class TwelveDataApiGuard
{
    public static void ThrowIfError(JsonElement root)
    {
        string status = GetString(root, "status");
        if (!string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string message = GetString(root, "message");
        string code = GetString(root, "code");
        string normalized = string.IsNullOrWhiteSpace(message)
            ? "Twelve Data вернул ошибку."
            : message;

        if (normalized.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("api key", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || code == "401")
        {
            throw new UnauthorizedAccessException(normalized);
        }

        throw new InvalidOperationException(normalized);
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        return value.ToString();
    }
}
