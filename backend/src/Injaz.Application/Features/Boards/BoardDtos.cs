using Injaz.Domain.Boards;
using Injaz.Domain.Cards;
using Injaz.Domain.Workspaces;

namespace Injaz.Application.Features.Boards;

public sealed record BoardSummaryDto(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    string? Description,
    string Color,
    bool IsArchived,
    DateTimeOffset CreatedAt)
{
    public static BoardSummaryDto From(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        return new(board.Id, board.WorkspaceId, board.Title, board.Description, board.Color, board.IsArchived, board.CreatedAt);
    }
}

/// <summary>
/// Everything the board screen needs in one response: open lists in order, each with its open cards in order,
/// plus the board's labels and the workspace members (for assignee pickers and avatars).
/// </summary>
public sealed record BoardDto(
    BoardSummaryDto Board,
    WorkspaceRole MyRole,
    IReadOnlyList<ListDto> Lists,
    IReadOnlyList<LabelDto> Labels,
    IReadOnlyList<BoardMemberDto> Members);

public sealed record ListDto(Guid Id, Guid BoardId, string Title, string Rank, bool IsArchived, IReadOnlyList<CardSummaryDto> Cards)
{
    public static ListDto From(BoardList list, IReadOnlyList<CardSummaryDto>? cards = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        return new(list.Id, list.BoardId, list.Title, list.Rank, list.IsArchived, cards ?? []);
    }
}

/// <summary>What a card tile shows. The full card (description, checklist, …) comes from <c>GET /api/cards/{id}</c>.</summary>
public sealed record CardSummaryDto(
    Guid Id,
    Guid ListId,
    string Title,
    string Rank,
    DateTimeOffset? DueAt,
    CardPriority Priority,
    DateTimeOffset? CompletedAt,
    bool HasDescription,
    IReadOnlyList<Guid> LabelIds,
    IReadOnlyList<Guid> AssigneeIds,
    int ChecklistDone,
    int ChecklistTotal,
    int CommentCount)
{
    /// <summary>From a tracked entity (commands); listing queries use <c>CardQueries.Summary</c> instead.</summary>
    public static CardSummaryDto From(Card card, Cards.CardBadges badges)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new(
            card.Id,
            card.ListId,
            card.Title,
            card.Rank,
            card.DueAt,
            card.Priority,
            card.CompletedAt,
            card.Description is not null,
            [.. card.Labels.Select(l => l.LabelId)],
            [.. card.Assignees.Select(a => a.UserId)],
            badges.ChecklistDone,
            badges.ChecklistTotal,
            badges.CommentCount);
    }
}

public sealed record LabelDto(Guid Id, string? Name, string Color)
{
    public static LabelDto From(Label label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return new(label.Id, label.Name, label.Color);
    }
}

public sealed record BoardMemberDto(Guid UserId, string DisplayName, string? AvatarUrl, WorkspaceRole Role);
