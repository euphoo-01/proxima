namespace Proxima.Application.Observability;

public sealed record AuditEvent(
    Guid Id,
    Guid OwnerUserId,
    DateTimeOffset OccurredAtUtc,
    string EventType,
    string Outcome,
    string Metadata);
