using Proxima.Application.Observability;

namespace Proxima.Infrastructure.Persistence;

public static class ProximaAuditComposition
{
    public static IAuditService CreateAuditService(string filePath)
    {
        return new AuditService(new JsonAuditLogRepository(filePath));
    }

    public static string GetDefaultAuditLogPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Proxima", "audit-log.json");
    }
}
