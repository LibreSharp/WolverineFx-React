using System.Security.Claims;
using ApiHost.Identity;
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;

namespace ApiHost.Features.Admin;

// One admin action: who did what to what, and when. The back office records every change an
// admin makes (locking an account, giving a plan, replaying a failed message...), so there's an
// answer to "who changed this?". Entries are never changed or deleted by the app.
public class AuditEntry
{
    public Guid Id { get; set; }

    public DateTimeOffset At { get; set; }

    public string ActorId { get; set; } = string.Empty;

    // The admin's email at the time, so the entry still reads well after their account is gone.
    public string ActorEmail { get; set; } = string.Empty;

    // One of AuditActions.
    public string Action { get; set; } = string.Empty;

    // "User", "Organisation", "Feature" or "Message", and its id.
    public string TargetType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    // What changed, for a person to read ("Roles: Admin", "Pro until 2026-12-31").
    public string? Details { get; set; }
}

public static class AuditActions
{
    public const string InviteUser = "user.invite";
    public const string LockUser = "user.lock";
    public const string UnlockUser = "user.unlock";
    public const string SetRoles = "user.roles";
    public const string EndSessions = "user.end-sessions";
    public const string ResendConfirmation = "user.resend-confirmation";
    public const string GrantPlan = "organisation.grant-plan";
    public const string Suspend = "organisation.suspend";
    public const string Unsuspend = "organisation.unsuspend";
    public const string EditNotes = "organisation.notes";
    public const string DeleteOrganisation = "organisation.delete";
    public const string SetFeature = "feature.set";
    public const string ReplayMessages = "messages.replay";
    public const string DiscardMessages = "messages.discard";
}

// The audit log, in Marten or EF Core (AuditLog). Each entry is saved at once.
public interface IAuditLog
{
    Task RecordAsync(ClaimsPrincipal actor, string action, string targetType, string targetId, string? details, CancellationToken cancellationToken);

    // Newest first, optionally only the entries about one target.
    Task<AuditPage> ListAsync(int page, string? targetId, CancellationToken cancellationToken);
}

public record AuditPage(IReadOnlyList<AuditEntry> Entries, int Total, int Page, int PageSize);

public static class AuditLogEndpoint
{
    public const int PageSize = 50;

    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/audit")]
    public static Task<AuditPage> Get(int? page, string? targetId, IAuditLog auditLog, CancellationToken cancellationToken)
        => auditLog.ListAsync(Math.Max(page ?? 1, 1), string.IsNullOrWhiteSpace(targetId) ? null : targetId.Trim(), cancellationToken);
}
