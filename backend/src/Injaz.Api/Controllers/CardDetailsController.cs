using Injaz.Application.Common;
using Injaz.Application.Features.Activity;
using Injaz.Application.Features.Cards;
using Injaz.Application.Features.Checklist;
using Injaz.Application.Features.Comments;
using Injaz.Application.Features.MyTasks;
using Injaz.Application.Messaging;
using Injaz.Domain.Cards;
using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.Controllers;

/// <summary>Checklist, comments and activity of a card; board activity and search; the caller's tasks.</summary>
[Route("api")]
public sealed class CardDetailsController(IDispatcher dispatcher) : ApiControllerBase
{
    // ---------- checklist ----------

    [HttpPost("cards/{cardId:guid}/checklist")]
    [ProducesResponseType<ChecklistItemDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddChecklistItemAsync(Guid cardId, ChecklistItemBody body, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await dispatcher.SendAsync(new AddChecklistItem(cardId, body.Text), cancellationToken));

    [HttpPatch("checklist-items/{itemId:guid}")]
    public Task<ChecklistItemDto> UpdateChecklistItemAsync(Guid itemId, UpdateChecklistItemBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new UpdateChecklistItem(itemId, body.Text, body.IsDone), cancellationToken);

    [HttpPost("checklist-items/{itemId:guid}/move")]
    public Task<ChecklistItemDto> MoveChecklistItemAsync(Guid itemId, MoveBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new MoveChecklistItem(itemId, body.AfterId, body.BeforeId), cancellationToken);

    [HttpDelete("checklist-items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteChecklistItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new DeleteChecklistItem(itemId), cancellationToken);
        return NoContent();
    }

    // ---------- comments ----------

    [HttpGet("cards/{cardId:guid}/comments")]
    public Task<PagedResult<CommentDto>> ListCommentsAsync(Guid cardId, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new ListComments(cardId, cursor, limit), cancellationToken);

    [HttpPost("cards/{cardId:guid}/comments")]
    [ProducesResponseType<CommentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddCommentAsync(Guid cardId, CommentBody body, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await dispatcher.SendAsync(new AddComment(cardId, body.Body), cancellationToken));

    [HttpPatch("comments/{commentId:guid}")]
    public Task<CommentDto> EditCommentAsync(Guid commentId, CommentBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new EditComment(commentId, body.Body), cancellationToken);

    [HttpDelete("comments/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new DeleteComment(commentId), cancellationToken);
        return NoContent();
    }

    // ---------- activity ----------

    [HttpGet("boards/{boardId:guid}/activity")]
    public Task<PagedResult<ActivityDto>> BoardActivityAsync(Guid boardId, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new GetBoardActivity(boardId, cursor, limit), cancellationToken);

    [HttpGet("cards/{cardId:guid}/activity")]
    public Task<PagedResult<ActivityDto>> CardActivityAsync(Guid cardId, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new GetCardActivity(cardId, cursor, limit), cancellationToken);

    // ---------- search & my tasks ----------

    /// <summary>Search a board's cards: <c>?q=&amp;labelIds=&amp;assigneeIds=&amp;due=overdue|today|week|none&amp;tz=&amp;archived=&amp;cursor=&amp;limit=</c>.</summary>
    [HttpGet("boards/{boardId:guid}/cards")]
    public Task<PagedResult<CardSearchResultDto>> SearchAsync(Guid boardId, [FromQuery] SearchCardsQuery query, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(
            new SearchCards(boardId, query.Q, query.LabelIds, query.AssigneeIds, query.Due, query.Tz, query.Archived, query.Cursor, query.Limit),
            cancellationToken);

    /// <summary>Cards assigned to me: <c>?due=&amp;priority=&amp;workspaceId=&amp;includeCompleted=&amp;tz=&amp;cursor=&amp;limit=</c>.</summary>
    [HttpGet("me/tasks")]
    public Task<PagedResult<MyTaskDto>> MyTasksAsync([FromQuery] MyTasksQuery query, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(
            new GetMyTasks(query.Due, query.Priority, query.WorkspaceId, query.IncludeCompleted, query.Tz, query.Cursor, query.Limit),
            cancellationToken);
}

public sealed record ChecklistItemBody(string? Text);

public sealed record UpdateChecklistItemBody(Optional<string?> Text, Optional<bool?> IsDone);

public sealed record CommentBody(string? Body);

public sealed class SearchCardsQuery
{
    public string? Q { get; init; }

    public Guid[]? LabelIds { get; init; }

    public Guid[]? AssigneeIds { get; init; }

    public DueBucket? Due { get; init; }

    public string? Tz { get; init; }

    public bool Archived { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

public sealed class MyTasksQuery
{
    public DueBucket? Due { get; init; }

    public CardPriority? Priority { get; init; }

    public Guid? WorkspaceId { get; init; }

    public bool IncludeCompleted { get; init; }

    public string? Tz { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
