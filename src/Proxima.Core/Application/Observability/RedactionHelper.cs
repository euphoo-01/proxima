namespace Proxima.Core.Application.Observability;

public static class RedactionHelper
{
    private static readonly string[] SensitiveKeys =
    [
        "password",
        "apikey",
        "api_key",
        "token",
        "secret",
    ];

    public static string Redact(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string result = text;
        foreach (string key in SensitiveKeys)
        {
            result = result.Replace(key, "[REDACTED]", StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
