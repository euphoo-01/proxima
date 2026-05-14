using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Proxima.Infrastructure.Persistence;

public sealed class ProximaDbContextFactory : IDesignTimeDbContextFactory<ProximaDbContext>
{
    public ProximaDbContext CreateDbContext(string[] args)
    {
        DatabaseOptions options = DatabaseConnectionStringProvider.Resolve();
        DbContextOptionsBuilder<ProximaDbContext> builder = new();
        DatabaseBootstrapService.ConfigureNpgsql(builder, options.ConnectionString);
        return new ProximaDbContext(builder.Options);
    }
}
