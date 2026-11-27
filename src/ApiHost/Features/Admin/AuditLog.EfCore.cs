using System.Security.Claims;
using ApiHost.Data.EfCore;
using ApiHost.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Features.Admin;

// Audit entries are a plain table; see Data/EfCore/AppDbContext.cs.
public class AuditLog(AppDbContext db, TimeProvider clock) : IAuditLog
{
    public async Task RecordAsync(ClaimsPrincipal actor, string action, string targetType, string targetId, string? details, CancellationToken cancellationToken)
    {
        db.AuditEntries.Add(new AuditEntry
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
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditPage> ListAsync(int page, string? targetId, CancellationToken cancellationToken)
    {
        var query = db.AuditEntries.AsNoTracking();
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
