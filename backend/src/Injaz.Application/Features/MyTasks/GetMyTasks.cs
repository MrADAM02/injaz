using Injaz.Application.Common;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Cards;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.MyTasks;

/// <summary>A card assigned to the caller, with enough context to show where it lives.</summary>
public sealed record MyTaskDto(
    CardSummaryDto Card,
    Guid WorkspaceId,
    string WorkspaceName,
    Guid BoardId,
    string BoardTitle,
    string BoardColor,
    string ListTitle);

/// <summary>
/// Everything assigned to me across all my workspaces: open cards on open lists and boards, soonest due first
/// (no due date last). <paramref name="Tz"/> is the caller's IANA time zone, used for "today"/"week".
/// </summary>
public sealed record GetMyTasks(
    DueBucket? Due,
    CardPriority? Priority,
    Guid? WorkspaceId,
    bool IncludeCompleted,
    string? Tz,
    string? Cursor,
    int? Limit) : IQuery<PagedResult<MyTaskDto>>, IAuthenticatedRequest;

internal sealed class GetMyTasksValidator : IValidator<GetMyTasks>
{
    public void Validate(GetMyTasks request, ValidationContext context)
    {
        DueRules.Validate(context, request.Due, request.Tz);
        context.RuleFor(nameof(request.Priority), request.Priority).IsInEnum();
        PageRules.Limit(context, request.Limit);
    }
}

internal sealed class GetMyTasksHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IQueryHandler<GetMyTasks, PagedResult<MyTaskDto>>
{
    public async Task<PagedResult<MyTaskDto>> HandleAsync(GetMyTasks query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var take = query.Limit ?? PageRules.DefaultLimit;

        var cards = db.Cards
            .Where(c => !c.IsArchived && c.Assignees.Any(a => a.UserId == userId))
            .WhereDue(query.Due, query.Tz, clock.GetUtcNow());

        if (!query.IncludeCompleted)
        {
            cards = cards.Where(c => c.CompletedAt == null);
        }

        if (query.Priority is { } priority)
        {
            cards = cards.Where(c => c.Priority == priority);
        }

        // Keyset on (dueAt ASC NULLS LAST, id ASC).
        if (query.Cursor is not null)
        {
            var (at, id) = KeysetCursor.Decode(query.Cursor);
            cards = at is null
                ? cards.Where(c => c.DueAt == null && c.Id > id)
                : cards.Where(c => c.DueAt > at || (c.DueAt == at && c.Id > id) || c.DueAt == null);
        }

        var rows = await cards
            .Join(db.BoardLists.Where(l => !l.IsArchived), c => c.ListId, l => l.Id, (c, l) => new { Card = c, List = l })
            .Join(db.Boards.Where(b => !b.IsArchived), x => x.Card.BoardId, b => b.Id, (x, b) => new { x.Card, x.List, Board = b })
            .Join(db.Workspaces, x => x.Board.WorkspaceId, w => w.Id, (x, w) => new { x.Card, x.List, x.Board, Workspace = w })
            .Where(x => x.Workspace.Members.Any(m => m.UserId == userId) && (query.WorkspaceId == null || x.Workspace.Id == query.WorkspaceId))
            .OrderBy(x => x.Card.DueAt == null)
            .ThenBy(x => x.Card.DueAt)
            .ThenBy(x => x.Card.Id)
            .Take(take + 1)
            .Select(x => new
            {
                CardId = x.Card.Id,
                x.Card.DueAt,
                WorkspaceId = x.Workspace.Id,
                WorkspaceName = x.Workspace.Name,
                BoardId = x.Board.Id,
                BoardTitle = x.Board.Title,
                BoardColor = x.Board.Color,
                ListTitle = x.List.Title,
            })
            .ToListAsync(cancellationToken);

        var tiles = await db.SummariesAsync([.. rows.Select(r => r.CardId)], cancellationToken);

        return PagedResult<MyTaskDto>.From(
            rows,
            take,
            r => new MyTaskDto(tiles[r.CardId], r.WorkspaceId, r.WorkspaceName, r.BoardId, r.BoardTitle, r.BoardColor, r.ListTitle),
            r => new KeysetCursor(r.DueAt, r.CardId));
    }
}
