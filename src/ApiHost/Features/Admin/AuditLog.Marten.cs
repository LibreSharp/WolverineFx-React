using System.Security.Claims;
using ApiHost.Identity;
using Marten;

namespace ApiHost.Features.Admin;

// Audit entries are ordinary (not tenanted) Marten documents.
public class AuditLog(IDocumentSession session, TimeProvider clock) : IAuditLog
{
    public Task RecordAsync(ClaimsPrincipal actor, string action, string targetType, string targetId, string? details, CancellationToken cancellationToken)
    {
        session.Store(new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            At = clock.GetUtcNow(),
            ActorId = actor.GetUserId(),
            ActorEmail = actor.FindFirstValue(ClaimTypes.Email) ?? actor.FindFirstValue("email") ?? string.Empty,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Details = details,
        });
        return session.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditPage> ListAsync(int page, string? targetId, CancellationToken cancellationToken)
    {
        var query = session.Query<AuditEntry>().AsQueryable();
        if (targetId is not null)
        {
            query = query.Where(e => e.TargetId == targetId);
        }

        var total = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(e => e.At)
            .Skip((page - 1) * AuditLogEndpoint.PageSize).Take(AuditLogEndpoint.PageSize)
            .ToListAsync(cancellationToken);
        return new AuditPage(entries, total, page, AuditLogEndpoint.PageSize);
    }
}
