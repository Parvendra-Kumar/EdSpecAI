namespace EdSpec.Domain.Audit;

public sealed record AuditLogEntry(
    string Id,
    string EventType,
    string EntityType,
    string EntityId,
    string Message,
    string Actor,
    DateTimeOffset CreatedAt);
