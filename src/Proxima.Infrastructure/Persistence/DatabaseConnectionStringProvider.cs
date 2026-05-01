namespace Proxima.Infrastructure.Persistence;

public static class DatabaseConnectionStringProvider
{
    private const string EnvName = "PROXIMA_DB_CONNECTION";

    public static DatabaseOptions Resolve()
    {
        string? raw = Environment.GetEnvironmentVariable(EnvName);
        string fallback = "Host=localhost;Port=55432;Database=proxima;Username=proxima;Password=proxima";
        string conn = string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
        bool seed = !string.Equals(Environment.GetEnvironmentVariable("PROXIMA_DB_SEED"), "false", StringComparison.OrdinalIgnoreCase);
        return new DatabaseOptions(conn, seed);
    }
}
