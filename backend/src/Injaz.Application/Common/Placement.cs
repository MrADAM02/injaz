using Injaz.Application.Common.Exceptions;
using Injaz.Domain.Ordering;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Common;

/// <summary>
/// Turns "put it after X / before Y" into a rank for one item in an ordered sequence (lists on a board, cards
/// in a list, …). Normally only the moved row changes. If the neighbours leave no room — keys grew past
/// <see cref="FractionalIndex.MaxLength"/>, or two concurrent moves produced equal ranks — that one sequence is
/// re-spread in the same transaction: a rare, bounded fallback, never a global reindex.
/// </summary>
internal static class Placement
{
    /// <param name="siblings">The target sequence, tracked (rebalancing updates these entities).</param>
    /// <param name="movingId">The item being placed (excluded from the siblings), or null for a new item.</param>
    /// <param name="afterId">Sibling to end up immediately before the item. Both ids null = append at the end.</param>
    /// <param name="beforeId">Sibling to end up immediately after the item.</param>
    /// <exception cref="ConflictException">A neighbour is no longer in the sequence: the client's view is stale.</exception>
    public static async Task<string> RankAsync<T>(
        IQueryable<T> siblings, Guid? movingId, Guid? afterId, Guid? beforeId, CancellationToken cancellationToken)
        where T : class, IRanked
    {
        if (movingId is { } moving)
        {
            siblings = siblings.Where(s => s.Id != moving);
        }

        if (afterId is not null && afterId == beforeId)
        {
            throw StaleNeighbor();
        }

        var afterRank = afterId is { } a ? await RankOfAsync(siblings, a, cancellationToken) : null;
        var beforeRank = beforeId is { } b ? await RankOfAsync(siblings, b, cancellationToken) : null;

        // Fill in whichever side the client didn't send.
        if (afterId is not null && beforeId is null)
        {
            beforeRank = await siblings.Where(s => string.Compare(s.Rank, afterRank) > 0)
                .OrderBy(s => s.Rank).Select(s => s.Rank).FirstOrDefaultAsync(cancellationToken);
        }
        else if (afterId is null && beforeId is not null)
        {
            afterRank = await siblings.Where(s => string.Compare(s.Rank, beforeRank) < 0)
                .OrderByDescending(s => s.Rank).Select(s => s.Rank).FirstOrDefaultAsync(cancellationToken);
        }
        else if (afterId is null && beforeId is null)
        {
            afterRank = await siblings.OrderByDescending(s => s.Rank).Select(s => s.Rank).FirstOrDefaultAsync(cancellationToken);
        }

        if (afterRank is null || beforeRank is null || string.CompareOrdinal(afterRank, beforeRank) < 0)
        {
            var rank = FractionalIndex.Between(afterRank, beforeRank);
            if (rank.Length <= FractionalIndex.MaxLength)
            {
                return rank;
            }
        }

        return await RebalanceAsync(siblings, afterId, beforeId, cancellationToken);
    }

    /// <summary>Re-spreads the whole sequence, leaving a slot at the requested position, and returns that slot's rank.</summary>
    private static async Task<string> RebalanceAsync<T>(IQueryable<T> siblings, Guid? afterId, Guid? beforeId, CancellationToken cancellationToken)
        where T : class, IRanked
    {
        var items = await siblings.OrderBy(s => s.Rank).ThenBy(s => s.Id).ToListAsync(cancellationToken);

        var slot = afterId is { } a ? items.FindIndex(i => i.Id == a) + 1
            : beforeId is { } b ? items.FindIndex(i => i.Id == b)
            : items.Count;

        var keys = FractionalIndex.Spread(items.Count + 1);
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Rerank(keys[i < slot ? i : i + 1]);
        }

        return keys[slot];
    }

    private static async Task<string> RankOfAsync<T>(IQueryable<T> siblings, Guid id, CancellationToken cancellationToken)
        where T : class, IRanked =>
        await siblings.Where(s => s.Id == id).Select(s => s.Rank).SingleOrDefaultAsync(cancellationToken)
        ?? throw StaleNeighbor();

    private static ConflictException StaleNeighbor() =>
        new("ordering.staleNeighbor", "The position you dropped at has changed. Reload and try again.");
}
