using System.Linq.Expressions;
using Injaz.Application.Common;
using Injaz.Application.Features.Boards;
using Injaz.Application.Persistence;
using Injaz.Domain.Cards;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Cards;

/// <summary>Card counts shown on a tile.</summary>
public readonly record struct CardBadges(int ChecklistDone, int ChecklistTotal, int CommentCount)
{
    public static CardBadges None => default;
}

internal static class CardQueries
{
    /// <summary>The single definition of a card tile, translated to SQL wherever tiles are listed.</summary>
    public static Expression<Func<Card, CardSummaryDto>> Summary(IAppDbContext db) => c => new CardSummaryDto(
        c.Id,
        c.ListId,
        c.Title,
        c.Rank,
        c.DueAt,
        c.Priority,
        c.CompletedAt,
        c.Description != null,
        c.Labels.Select(l => l.LabelId).ToList(),
        c.Assignees.Select(a => a.UserId).ToList(),
        db.ChecklistItems.Count(i => i.CardId == c.Id && i.IsDone),
        db.ChecklistItems.Count(i => i.CardId == c.Id),
        db.Comments.Count(x => x.CardId == c.Id));

    public static async Task<CardBadges> BadgesAsync(this IAppDbContext db, Guid cardId, CancellationToken cancellationToken)
    {
        var done = await db.ChecklistItems.CountAsync(i => i.CardId == cardId && i.IsDone, cancellationToken);
        var total = await db.ChecklistItems.CountAsync(i => i.CardId == cardId, cancellationToken);
        var comments = await db.Comments.CountAsync(c => c.CardId == cardId, cancellationToken);
        return new CardBadges(done, total, comments);
    }

    /// <summary>Second step of a two-step page: tiles for the ids of one page, keyed by id.</summary>
    public static async Task<Dictionary<Guid, CardSummaryDto>> SummariesAsync(
        this IAppDbContext db, IReadOnlyCollection<Guid> cardIds, CancellationToken cancellationToken) =>
        await db.Cards
            .AsNoTracking()
            .Where(c => cardIds.Contains(c.Id))
            .Select(Summary(db))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

    /// <summary>Applies a due bucket (see <see cref="DueWindow"/>) to a card query.</summary>
    public static IQueryable<Card> WhereDue(this IQueryable<Card> cards, DueBucket? due, string? timeZone, DateTimeOffset now)
    {
        if (due is null)
        {
            return cards;
        }

        var window = DueWindow.For(due.Value, now, timeZone);
        if (window.NoDueDate)
        {
            return cards.Where(c => c.DueAt == null);
        }

        if (window.From is { } from)
        {
            cards = cards.Where(c => c.DueAt >= from);
        }

        if (window.To is { } to)
        {
            cards = cards.Where(c => c.DueAt < to);
        }

        return window.ExcludeCompleted ? cards.Where(c => c.CompletedAt == null) : cards;
    }
}
