using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Proxima.Infrastructure.Persistence;

public sealed class DatabaseBootstrapService(DatabaseOptions options)
{
    public async Task<string> EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(options.ConnectionString);
            await using NpgsqlConnection conn = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using NpgsqlCommand cmd = new("select 1;", conn);
            await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            await ApplySqlScriptsAsync(conn, cancellationToken).ConfigureAwait(false);
            if (options.EnableSeed)
            {
                await ApplySeedAsync(conn, cancellationToken).ConfigureAwait(false);
            }

            return "Database ready.";
        }
        catch (Exception ex) when (ex is NpgsqlException or IOException or UnauthorizedAccessException or System.Net.Sockets.SocketException)
        {
            return $"Database unavailable: {ex.Message}";
        }
    }

    public ProximaDbContext CreateDbContext()
    {
        DbContextOptionsBuilder<ProximaDbContext> builder = new();
        builder.UseNpgsql(options.ConnectionString);
        return new ProximaDbContext(builder.Options);
    }

    private static async Task ApplySqlScriptsAsync(NpgsqlConnection conn, CancellationToken cancellationToken)
    {
        string root = FindRepoRoot();
        string script = Path.Combine(root, "scripts", "sql", "0001_initial_schema.sql");
        if (!File.Exists(script))
        {
            return;
        }

        string sql = await File.ReadAllTextAsync(script, cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand cmd = new(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplySeedAsync(NpgsqlConnection conn, CancellationToken cancellationToken)
    {
        string root = FindRepoRoot();
        string script = Path.Combine(root, "scripts", "sql", "0002_seed_demo.sql");
        if (!File.Exists(script))
        {
            return;
        }

        string sql = await File.ReadAllTextAsync(script, cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand cmd = new(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Proxima.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
