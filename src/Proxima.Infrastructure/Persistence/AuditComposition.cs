using Microsoft.Extensions.DependencyInjection;
using Proxima.Core.Application.Observability;
using Proxima.Infrastructure.Persistence.Repositories;

namespace Proxima.Infrastructure.Persistence;

public static class AuditComposition
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddSingleton<IAuditLogRepository, PostgresAuditLogRepository>();
        services.AddSingleton<IAuditService, AuditService>();
        return services;
    }
}
