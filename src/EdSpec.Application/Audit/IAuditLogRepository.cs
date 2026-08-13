using EdSpec.Domain.Audit;

namespace EdSpec.Application.Audit;

public interface IAuditLogRepository
{
    Task<AuditLogEntry> CreateAsync(AuditLogEntry entry, CancellationToken cancellationToken);
}
