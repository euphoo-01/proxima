namespace Proxima.Infrastructure.Persistence;

public sealed record DatabaseOptions(string ConnectionString, bool EnableSeed);
