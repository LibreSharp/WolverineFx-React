using System.Security.Claims;
using ApiHost.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;
using Wolverine.Persistence.Durability.DeadLetterManagement;
using Wolverine.Runtime;

namespace ApiHost.Features.Admin;

// A message that failed every attempt and was parked in Wolverine's dead-letter queue (in
// PostgreSQL): an email that couldn't be sent, a payment webhook that errored...
public record FailedMessage(
    Guid Id,
    string MessageType,
    string? ExceptionType,
    string? ExceptionMessage,
    DateTimeOffset SentAt,
    DateTimeOffset? FailedAt,
    string? Source,
    bool Replayable,
    int ReplayCount);

public record FailedMessagePage(IReadOnlyList<FailedMessage> Messages, int Total, int Page, int PageSize);

public record FailedMessagesRequest(IReadOnlyList<Guid> Ids);

public class FailedMessagesRequestValidator : AbstractValidator<FailedMessagesRequest>
{
    public FailedMessagesRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(ids => ids.Count <= 100).WithMessage("At most 100 at a time.");
    }
}

// The back office's failed messages: see why a message failed, then replay it (once the cause is
// fixed) or discard it.
public static class FailedMessageEndpoints
{
    public const int PageSize = 25;

    [Authorize(Roles = AppRoles.Admin)]
    [WolverineGet("/api/admin/failed-messages")]
    public static async Task<FailedMessagePage> List(int? page, IWolverineRuntime runtime, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(page ?? 1, 1);
        var results = await runtime.Storage.DeadLetters.QueryAsync(
            new DeadLetterEnvelopeQuery { PageNumber = pageNumber, PageSize = PageSize, Archived = DeadLetterArchiveFilter.Exclude },
            cancellationToken);
        return new FailedMessagePage(
            [.. results.Envelopes.Select(e => new FailedMessage(
                e.Id, e.MessageType, e.ExceptionType, e.ExceptionMessage, e.SentAt, e.FailedAt, e.Source, e.Replayable, e.ReplayCount))],
            results.TotalCount,
            pageNumber,
            PageSize);
    }

    // Sends the messages to their handlers again.
    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePost("/api/admin/failed-messages/replay")]
    public static async Task<IResult> Replay(
        FailedMessagesRequest request, ClaimsPrincipal principal, IWolverineRuntime runtime, IAuditLog auditLog, CancellationToken cancellationToken)
    {
        await runtime.Storage.DeadLetters.ReplayAsync(new DeadLetterEnvelopeQuery { MessageIds = [.. request.Ids] }, cancellationToken);
        await auditLog.RecordAsync(principal, AuditActions.ReplayMessages, "Message", string.Join(",", request.Ids), $"{request.Ids.Count} message(s)", cancellationToken);
        return TypedResults.NoContent();
    }

    [Authorize(Roles = AppRoles.Admin)]
    [WolverinePost("/api/admin/failed-messages/discard")]
    public static async Task<IResult> Discard(
        FailedMessagesRequest request, ClaimsPrincipal principal, IWolverineRuntime runtime, IAuditLog auditLog, CancellationToken cancellationToken)
    {
        await runtime.Storage.DeadLetters.DiscardAsync(new DeadLetterEnvelopeQuery { MessageIds = [.. request.Ids] }, cancellationToken);
        await auditLog.RecordAsync(principal, AuditActions.DiscardMessages, "Message", string.Join(",", request.Ids), $"{request.Ids.Count} message(s)", cancellationToken);
        return TypedResults.NoContent();
    }
}
