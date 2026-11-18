namespace Proxima.Core.Application.Database;

public sealed record DatabaseServerInfo(string DatabaseName, string SessionUser, string ServerTime);
public sealed record CatalogCurrency(string Code, string Name);
public sealed record CatalogInstrument(string Ticker, string Name, string CurrencyCode, string InstrumentType);
public sealed record DatabaseCatalogSnapshot(
    DatabaseServerInfo Server,
    IReadOnlyList<CatalogCurrency> Currencies,
    IReadOnlyList<CatalogInstrument> Instruments);

public interface IDatabaseCatalog
{
    Task<DatabaseCatalogSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed class DatabaseCatalogNotConfiguredException() : InvalidOperationException("Database catalog connection is not configured.");
