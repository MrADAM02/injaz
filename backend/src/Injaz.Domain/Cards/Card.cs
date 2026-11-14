using Injaz.Domain.Common;
using Injaz.Domain.Ordering;

namespace Injaz.Domain.Cards;

/// <summary>
/// A card (task). Aggregate root for its assignees and labels. <see cref="BoardId"/> is denormalized from the
/// list so board views, filters and authorization never need to join through lists.
/// </summary>
public sealed class Card : Entity, IRanked
{
    public const int TitleMaxLength = 500;
    public const int DescriptionMaxLength = 20_000;

    private readonly List<CardAssignee> _assignees = [];
    private readonly List<CardLabel> _labels = [];

    private Card()
    {
        // EF Core
    }

    public Guid BoardId { get; private set; }

    public Guid ListId { get; private set; }

    public string Title { get; private set; } = null!;

    /// <summary>Markdown source. Rendered (and sanitized) by the client.</summary>
    public string? Description { get; private set; }

    public string Rank { get; private set; } = null!;

    public DateTimeOffset? DueAt { get; private set; }

    public CardPriority Priority { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsArchived { get; private set; }

    public Guid CreatedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<CardAssignee> Assignees => _assignees.AsReadOnly();

    public IReadOnlyCollection<CardLabel> Labels => _labels.AsReadOnly();

    public bool IsCompleted => CompletedAt is not null;

    public static Card Create(Guid boardId, Guid listId, string title, string rank, Guid createdById, DateTimeOffset now) =>
        new()
        {
            BoardId = boardId,
            ListId = listId,
            Title = title.Trim(),
            Rank = rank,
            CreatedById = createdById,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void Rename(string title, DateTimeOffset now) => Touch(() => Title = title.Trim(), now);

    public void Describe(string? description, DateTimeOffset now) =>
        Touch(() => Description = string.IsNullOrWhiteSpace(description) ? null : description, now);

    /// <summary>Stored in UTC (PostgreSQL timestamptz); null clears the due date.</summary>
    public void Schedule(DateTimeOffset? dueAt, DateTimeOffset now) => Touch(() => DueAt = dueAt?.ToUniversalTime(), now);

    public void Prioritize(CardPriority priority, DateTimeOffset now) => Touch(() => Priority = priority, now);

    /// <summary>Idempotent: completing an already-completed card keeps the original completion time.</summary>
    public void SetCompleted(bool completed, DateTimeOffset now) =>
        Touch(() => CompletedAt = completed ? CompletedAt ?? now : null, now);

    public void SetArchived(bool archived, DateTimeOffset now) => Touch(() => IsArchived = archived, now);

    /// <summary>Moves within or across lists of the same board. Cross-board moves are not supported (yet).</summary>
    public void MoveTo(Guid listId, string rank, DateTimeOffset now) =>
        Touch(
            () =>
            {
                ListId = listId;
                Rank = rank;
            },
            now);

    public void Rerank(string rank) => Rank = rank;

    /// <returns>False when the user was already assigned (PUT semantics: idempotent).</returns>
    public bool Assign(Guid userId, DateTimeOffset now)
    {
        if (_assignees.Exists(a => a.UserId == userId))
        {
            return false;
        }

        _assignees.Add(new CardAssignee(Id, userId, now));
        UpdatedAt = now;
        return true;
    }

    /// <returns>False when the user wasn't assigned (DELETE semantics: idempotent).</returns>
    public bool Unassign(Guid userId, DateTimeOffset now)
    {
        var removed = _assignees.RemoveAll(a => a.UserId == userId) > 0;
        if (removed)
        {
            UpdatedAt = now;
        }

        return removed;
    }

    public bool AddLabel(Guid labelId, DateTimeOffset now)
    {
        if (_labels.Exists(l => l.LabelId == labelId))
        {
            return false;
        }

        _labels.Add(new CardLabel(Id, labelId));
        UpdatedAt = now;
        return true;
    }

    public bool RemoveLabel(Guid labelId, DateTimeOffset now)
    {
        var removed = _labels.RemoveAll(l => l.LabelId == labelId) > 0;
        if (removed)
        {
            UpdatedAt = now;
        }

        return removed;
    }

    private void Touch(Action change, DateTimeOffset now)
    {
        change();
        UpdatedAt = now;
    }
}

public enum CardPriority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4,
}

public sealed class CardAssignee
{
    private CardAssignee()
    {
        // EF Core
    }

    internal CardAssignee(Guid cardId, Guid userId, DateTimeOffset assignedAt)
    {
        CardId = cardId;
        UserId = userId;
        AssignedAt = assignedAt;
    }

    public Guid CardId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }
}

public sealed class CardLabel
{
    private CardLabel()
    {
        // EF Core
    }

    internal CardLabel(Guid cardId, Guid labelId)
    {
        CardId = cardId;
        LabelId = labelId;
    }

    public Guid CardId { get; private set; }

    public Guid LabelId { get; private set; }
}
