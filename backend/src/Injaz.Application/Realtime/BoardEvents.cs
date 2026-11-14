using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Realtime;

/// <summary>
/// What a live board client receives. <see cref="Type"/> is one of <see cref="BoardEventTypes"/>; <see cref="Payload"/>
/// is the fresh DTO (for upserts) or <c>{ id }</c> (for removals), so clients patch state without refetching.
/// </summary>
public sealed record BoardEvent(string Type, Guid BoardId, Guid ActorId, object Payload, DateTimeOffset OccurredAt);

public static class BoardEventTypes
{
    public const string BoardUpdated = "board.updated";
    public const string BoardDeleted = "board.deleted";
    public const string ListUpserted = "list.upserted";
    public const string ListRemoved = "list.removed";
    public const string CardUpserted = "card.upserted";
    public const string CardRemoved = "card.removed";
    public const string LabelUpserted = "label.upserted";
    public const string LabelRemoved = "label.removed";
}

/// <summary>Transport for board events (SignalR in the API). Implementations must not throw for absent listeners.</summary>
public interface IRealtimePublisher
{
    /// <summary>Sends to everyone watching the board, except the connection that caused the change.</summary>
    Task PublishAsync(BoardEvent boardEvent, CancellationToken cancellationToken);

    /// <summary>Stops a removed member's live connections from receiving these boards and tells their clients.</summary>
    Task RevokeAsync(Guid userId, Guid workspaceId, IReadOnlyCollection<Guid> boardIds, CancellationToken cancellationToken);
}

/// <summary>
/// Handlers report *what* changed ("card X on board Y"); after the commit, one flush per request reloads the
/// committed state and publishes one event per changed item. This keeps handlers simple, deduplicates repeated
/// changes (a PATCH touching five fields → one event), turns archived items into removals, and guarantees that
/// nothing is broadcast for a command that rolled back.
/// </summary>
public interface IBoardEvents
{
    void BoardChanged(Guid boardId);

    void BoardDeleted(Guid boardId);

    void ListChanged(Guid boardId, Guid listId);

    void ListDeleted(Guid boardId, Guid listId);

    /// <summary>Anything shown on the tile or in the card dialog (fields, labels, assignees, checklist, comments).</summary>
    void CardChanged(Guid boardId, Guid cardId);

    void CardDeleted(Guid boardId, Guid cardId);

    void LabelChanged(Guid boardId, Guid labelId);

    void LabelDeleted(Guid boardId, Guid labelId);

    void AccessRevoked(Guid workspaceId, Guid userId);
}

internal sealed class BoardEvents(
    IAppDbContext db,
    ICurrentUser currentUser,
    IRealtimePublisher publisher,
    IAfterCommit afterCommit,
    TimeProvider clock) : IBoardEvents
{
    private enum Topic
    {
        Board,
        List,
        Card,
        Label,
    }

    private readonly List<(Topic Topic, Guid BoardId, Guid Id)> _order = [];
    private readonly Dictionary<(Topic, Guid), bool> _deleted = [];
    private readonly List<(Guid WorkspaceId, Guid UserId)> _revocations = [];
    private bool _scheduled;

    public void BoardChanged(Guid boardId) => Track(Topic.Board, boardId, boardId, deleted: false);

    public void BoardDeleted(Guid boardId) => Track(Topic.Board, boardId, boardId, deleted: true);

    public void ListChanged(Guid boardId, Guid listId) => Track(Topic.List, boardId, listId, deleted: false);

    public void ListDeleted(Guid boardId, Guid listId) => Track(Topic.List, boardId, listId, deleted: true);

    public void CardChanged(Guid boardId, Guid cardId) => Track(Topic.Card, boardId, cardId, deleted: false);

    public void CardDeleted(Guid boardId, Guid cardId) => Track(Topic.Card, boardId, cardId, deleted: true);

    public void LabelChanged(Guid boardId, Guid labelId) => Track(Topic.Label, boardId, labelId, deleted: false);

    public void LabelDeleted(Guid boardId, Guid labelId) => Track(Topic.Label, boardId, labelId, deleted: true);

    public void AccessRevoked(Guid workspaceId, Guid userId)
    {
        _revocations.Add((workspaceId, userId));
        Schedule();
    }

    private void Track(Topic topic, Guid boardId, Guid id, bool deleted)
    {
        var key = (topic, id);
        if (_deleted.TryGetValue(key, out var wasDeleted))
        {
            _deleted[key] = wasDeleted || deleted; // a deletion always wins
        }
        else
        {
            _deleted[key] = deleted;
            _order.Add((topic, boardId, id));
        }

        Schedule();
    }

    private void Schedule()
    {
        if (!_scheduled)
        {
            _scheduled = true;
            afterCommit.Enqueue(FlushAsync);
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId;
        var now = clock.GetUtcNow();

        foreach (var (topic, boardId, id) in _order)
        {
            var (type, payload) = _deleted[(topic, id)]
                ? Removal(topic, id)
                : await CurrentStateAsync(topic, id, cancellationToken);

            await publisher.PublishAsync(new BoardEvent(type, boardId, actorId, payload, now), cancellationToken);
        }

        foreach (var (workspaceId, userId) in _revocations)
        {
            var boardIds = await db.Boards.Where(b => b.WorkspaceId == workspaceId).Select(b => b.Id).ToListAsync(cancellationToken);
            await publisher.RevokeAsync(userId, workspaceId, boardIds, cancellationToken);
        }
    }

    private static (string Type, object Payload) Removal(Topic topic, Guid id) => topic switch
    {
        Topic.Board => (BoardEventTypes.BoardDeleted, new { id }),
        Topic.List => (BoardEventTypes.ListRemoved, new { id }),
        Topic.Card => (BoardEventTypes.CardRemoved, new { id }),
        Topic.Label => (BoardEventTypes.LabelRemoved, new { id }),
        _ => throw new ArgumentOutOfRangeException(nameof(topic)),
    };

    /// <summary>Committed state. Archived lists/cards are removals from the live board's point of view.</summary>
    private async Task<(string Type, object Payload)> CurrentStateAsync(Topic topic, Guid id, CancellationToken cancellationToken)
    {
        object? state = topic switch
        {
            Topic.Board => await db.Boards.AsNoTracking().Where(b => b.Id == id)
                .Select(b => new BoardSummaryDto(b.Id, b.WorkspaceId, b.Title, b.Description, b.Color, b.IsArchived, b.CreatedAt))
                .SingleOrDefaultAsync(cancellationToken),
            Topic.List => await db.BoardLists.AsNoTracking().Where(l => l.Id == id && !l.IsArchived)
                .Select(l => new ListDto(l.Id, l.BoardId, l.Title, l.Rank, l.IsArchived, new List<CardSummaryDto>()))
                .SingleOrDefaultAsync(cancellationToken),
            Topic.Card => await db.Cards.AsNoTracking()
                .Where(c => c.Id == id && !c.IsArchived && db.BoardLists.Any(l => l.Id == c.ListId && !l.IsArchived))
                .Select(CardQueries.Summary(db))
                .SingleOrDefaultAsync(cancellationToken),
            Topic.Label => await db.Labels.AsNoTracking().Where(l => l.Id == id)
                .Select(l => new LabelDto(l.Id, l.Name, l.Color))
                .SingleOrDefaultAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(topic)),
        };

        if (state is null)
        {
            return Removal(topic, id);
        }

        return topic switch
        {
            Topic.Board => (BoardEventTypes.BoardUpdated, state),
            Topic.List => (BoardEventTypes.ListUpserted, state),
            Topic.Card => (BoardEventTypes.CardUpserted, state),
            Topic.Label => (BoardEventTypes.LabelUpserted, state),
            _ => throw new ArgumentOutOfRangeException(nameof(topic)),
        };
    }
}

// ---------- the hub's subscription check ----------

/// <summary>Used by the real-time hub before adding a connection to a board's group: same rule as reading the board.</summary>
public sealed record SubscribeToBoard(Guid BoardId) : Messaging.IQuery<Messaging.Unit>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Board(BoardId);

    public Permission Permission => Permission.View;
}

internal sealed class SubscribeToBoardHandler : Messaging.IQueryHandler<SubscribeToBoard, Messaging.Unit>
{
    public Task<Messaging.Unit> HandleAsync(SubscribeToBoard query, CancellationToken cancellationToken) =>
        Task.FromResult(Messaging.Unit.Value); // authorization is the whole point
}

/// <summary>Default for hosts without a real-time transport (and for tests that don't care).</summary>
internal sealed class NullRealtimePublisher : IRealtimePublisher
{
    public Task PublishAsync(BoardEvent boardEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RevokeAsync(Guid userId, Guid workspaceId, IReadOnlyCollection<Guid> boardIds, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
