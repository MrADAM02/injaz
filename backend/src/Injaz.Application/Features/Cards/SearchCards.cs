using Injaz.Application.Common;
using Injaz.Application.Features.Boards;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Cards;

public sealed record CardSearchResultDto(CardSummaryDto Card, string ListTitle, bool IsArchived, DateTimeOffset UpdatedAt);

/// <summary>
/// Server-side search on one board: text in title or description (case-insensitive), any of the given labels,
/// any of the given assignees, a due bucket, and open vs archived cards. Most recently updated first.
/// The board screen filters its already-loaded cards client-side; this endpoint covers text in descriptions and
/// archived cards, which the board payload doesn't include.
/// </summary>
public sealed record SearchCards(
    Guid BoardId,
    string? Q,
    IReadOnlyList<Guid>? LabelIds,
    IReadOnlyList<Guid>? AssigneeIds,
    DueBucket? Due,
    string? Tz,
    bool Archived,
    string? Cursor,
    int? Limit) : IQuery<PagedResult<CardSearchResultDto>>, IAuthorizedRequest
{
    public const int QueryMaxLength = 200;
    public const int MaxFilterIds = 50;

    public ResourceScope Scope => ResourceScope.Board(BoardId);

    public Permission Permission => Permission.View;
}

internal sealed class SearchCardsValidator : IValidator<SearchCards>
{
    public void Validate(SearchCards request, ValidationContext context)
    {
        context.RuleFor(nameof(request.Q), request.Q).MaxLength(SearchCards.QueryMaxLength);
        context.RuleFor(nameof(request.LabelIds), request.LabelIds?.Count ?? 0).InclusiveBetween(0, SearchCards.MaxFilterIds);
        context.RuleFor(nameof(request.AssigneeIds), request.AssigneeIds?.Count ?? 0).InclusiveBetween(0, SearchCards.MaxFilterIds);
        DueRules.Validate(context, request.Due, request.Tz);
        PageRules.Limit(context, request.Limit);
    }
}

internal sealed class SearchCardsHandler(IAppDbContext db, TimeProvider clock) : IQueryHandler<SearchCards, PagedResult<CardSearchResultDto>>
{
    public async Task<PagedResult<CardSearchResultDto>> HandleAsync(SearchCards query, CancellationToken cancellationToken)
    {
        var take = query.Limit ?? PageRules.DefaultLimit;
        var cards = db.Cards
            .Where(c => c.BoardId == query.BoardId && c.IsArchived == query.Archived)
            .WhereDue(query.Due, query.Tz, clock.GetUtcNow());

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            // Plain substring match (no wildcards from user input); fine at board scale. pg_trgm can back it later.
            var term = query.Q.Trim().ToLowerInvariant();
            cards = cards.Where(c => c.Title.ToLower().Contains(term) || (c.Description != null && c.Description.ToLower().Contains(term)));
        }

        if (query.LabelIds is { Count: > 0 } labelIds)
        {
            cards = cards.Where(c => c.Labels.Any(l => labelIds.Contains(l.LabelId)));
        }

        if (query.AssigneeIds is { Count: > 0 } assigneeIds)
        {
            cards = cards.Where(c => c.Assignees.Any(a => assigneeIds.Contains(a.UserId)));
        }

        // Keyset on (updatedAt DESC, id DESC).
        if (query.Cursor is not null)
        {
            var (at, id) = KeysetCursor.Decode(query.Cursor);
            cards = cards.Where(c => c.UpdatedAt < at || (c.UpdatedAt == at && c.Id < id));
        }

        var rows = await cards
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .Take(take + 1)
            .Join(db.BoardLists, c => c.ListId, l => l.Id, (c, l) => new { c.Id, c.UpdatedAt, c.IsArchived, ListTitle = l.Title })
            .ToListAsync(cancellationToken);

        rows = [.. rows.OrderByDescending(r => r.UpdatedAt).ThenByDescending(r => r.Id)];
        var tiles = await db.SummariesAsync([.. rows.Select(r => r.Id)], cancellationToken);

        return PagedResult<CardSearchResultDto>.From(
            rows,
            take,
            r => new CardSearchResultDto(tiles[r.Id], r.ListTitle, r.IsArchived, r.UpdatedAt),
            r => new KeysetCursor(r.UpdatedAt, r.Id));
    }
}
