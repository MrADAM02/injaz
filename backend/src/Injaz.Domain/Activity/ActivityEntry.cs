using Injaz.Domain.Common;

namespace Injaz.Domain.Activity;

/// <summary>
/// An append-only record of something that happened on a board. <see cref="Data"/> is a small JSON object with
/// the details needed to render the entry (names and titles *as they were*), so the feed reads correctly even
/// after things are renamed or deleted.
/// </summary>
public sealed class ActivityEntry : Entity
{
    public const int TypeMaxLength = 64;

    private ActivityEntry()
    {
        // EF Core
    }

    public Guid WorkspaceId { get; private set; }

    public Guid BoardId { get; private set; }

    /// <summary>Null for board/list-level events and for cards that no longer exist.</summary>
    public Guid? CardId { get; private set; }

    public Guid ActorId { get; private set; }

    /// <summary>One of <see cref="ActivityTypes"/>.</summary>
    public string Type { get; private set; } = null!;

    /// <summary>JSON object (stored as jsonb).</summary>
    public string Data { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public static ActivityEntry Create(Guid workspaceId, Guid boardId, Guid? cardId, Guid actorId, string type, string data, DateTimeOffset now) =>
        new()
        {
            WorkspaceId = workspaceId,
            BoardId = boardId,
            CardId = cardId,
            ActorId = actorId,
            Type = type,
            Data = data,
            CreatedAt = now,
        };
}

/// <summary>Stable event names shared with the frontend (which maps each to a localized sentence).</summary>
public static class ActivityTypes
{
    public const string BoardCreated = "board.created";
    public const string BoardRenamed = "board.renamed";
    public const string BoardArchived = "board.archived";
    public const string BoardRestored = "board.restored";

    public const string ListCreated = "list.created";
    public const string ListRenamed = "list.renamed";
    public const string ListArchived = "list.archived";
    public const string ListRestored = "list.restored";
    public const string ListDeleted = "list.deleted";

    public const string CardCreated = "card.created";
    public const string CardRenamed = "card.renamed";
    public const string CardDescriptionChanged = "card.descriptionChanged";
    public const string CardDueChanged = "card.dueChanged";
    public const string CardPriorityChanged = "card.priorityChanged";
    public const string CardCompleted = "card.completed";
    public const string CardReopened = "card.reopened";
    public const string CardArchived = "card.archived";
    public const string CardRestored = "card.restored";
    public const string CardMoved = "card.moved";
    public const string CardDeleted = "card.deleted";
    public const string CardAssigned = "card.assigned";
    public const string CardUnassigned = "card.unassigned";
    public const string CardLabelAdded = "card.labelAdded";
    public const string CardLabelRemoved = "card.labelRemoved";

    public const string ChecklistItemAdded = "checklist.itemAdded";
    public const string ChecklistItemCompleted = "checklist.itemCompleted";
    public const string ChecklistItemReopened = "checklist.itemReopened";
    public const string ChecklistItemDeleted = "checklist.itemDeleted";

    public const string CommentAdded = "comment.added";
}
