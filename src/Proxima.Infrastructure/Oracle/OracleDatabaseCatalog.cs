using System.Data;
using Oracle.ManagedDataAccess.Client;
using Proxima.Core.Application.Database;

namespace Proxima.Infrastructure.Oracle;

public sealed class OracleDatabaseCatalog : IDatabaseCatalog
{
    public async Task<DatabaseCatalogSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        string? connectionString = Environment.GetEnvironmentVariable("PROXIMA_ORACLE_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DatabaseCatalogNotConfiguredException();
        }

        OracleConnectionStringBuilder options = new(connectionString) { ConnectionTimeout = 10 };
        using OracleConnection connection = new(options.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        DatabaseServerInfo? server = null;
        using (OracleCommand command = CreateCommand(connection, "GET_SERVER_INFO"))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                server = new DatabaseServerInfo(reader.GetString(reader.GetOrdinal("DATABASE_NAME")),
                    reader.GetString(reader.GetOrdinal("SESSION_USER")),
                    reader.GetProviderSpecificValue(reader.GetOrdinal("SERVER_TIME")).ToString() ?? "");
            }
        }

        if (server is null)
        {
            throw new InvalidOperationException("Oracle server information was not returned.");
        }

        List<CatalogCurrency> currencies = [];
        using (OracleCommand command = CreateCommand(connection, "LIST_CURRENCIES"))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                currencies.Add(new CatalogCurrency(reader.GetString(reader.GetOrdinal("CODE")), reader.GetString(reader.GetOrdinal("NAME"))));
            }
        }

        List<CatalogInstrument> instruments = [];
        using (OracleCommand command = CreateCommand(connection, "LIST_INSTRUMENTS"))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                instruments.Add(new CatalogInstrument(reader.GetString(reader.GetOrdinal("TICKER")),
                    reader.GetString(reader.GetOrdinal("NAME")), reader.GetString(reader.GetOrdinal("CURRENCY_CODE")),
                    reader.GetString(reader.GetOrdinal("INSTRUMENT_TYPE"))));
            }
        }

        return new DatabaseCatalogSnapshot(server, currencies, instruments);
    }

    private static OracleCommand CreateCommand(OracleConnection connection, string procedure)
    {
        OracleCommand command = connection.CreateCommand();
        command.CommandText = $"PROXIMA_OWNER.PKG_REFERENCE.{procedure}";
        command.CommandType = CommandType.StoredProcedure;
        command.BindByName = true;
        command.CommandTimeout = 15;
        command.Parameters.Add("p_result", OracleDbType.RefCursor, ParameterDirection.Output);
        return command;
    }
}
