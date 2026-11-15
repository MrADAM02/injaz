using Injaz.Application.Common;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Lists;
using Injaz.Application.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.Controllers;

[Route("api")]
public sealed class BoardsController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet("workspaces/{workspaceId:guid}/boards")]
    public Task<IReadOnlyList<BoardSummaryDto>> ListAsync(Guid workspaceId, [FromQuery] bool includeArchived, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new ListBoards(workspaceId, includeArchived), cancellationToken);

    [HttpPost("workspaces/{workspaceId:guid}/boards")]
    [ProducesResponseType<BoardSummaryDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(Guid workspaceId, CreateBoardBody body, CancellationToken cancellationToken)
    {
        var board = await dispatcher.SendAsync(new CreateBoard(workspaceId, body.Title, body.Description, body.Color), cancellationToken);
        return Created($"/api/boards/{board.Id}", board);
    }

    /// <summary>The whole board: open lists with their open cards (in order), labels and members.</summary>
    [HttpGet("boards/{boardId:guid}")]
    public Task<BoardDto> GetAsync(Guid boardId, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new GetBoard(boardId), cancellationToken);

    [HttpPatch("boards/{boardId:guid}")]
    public Task<BoardSummaryDto> UpdateAsync(Guid boardId, UpdateBoardBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new UpdateBoard(boardId, body.Title, body.Description, body.Color, body.IsArchived), cancellationToken);

    [HttpDelete("boards/{boardId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid boardId, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new DeleteBoard(boardId), cancellationToken);
        return NoContent();
    }

    // ---------- labels ----------

    [HttpPost("boards/{boardId:guid}/labels")]
    [ProducesResponseType<LabelDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateLabelAsync(Guid boardId, CreateLabelBody body, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await dispatcher.SendAsync(new CreateLabel(boardId, body.Name, body.Color), cancellationToken));

    [HttpPatch("labels/{labelId:guid}")]
    public Task<LabelDto> UpdateLabelAsync(Guid labelId, UpdateLabelBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new UpdateLabel(labelId, body.Name, body.Color), cancellationToken);

    [HttpDelete("labels/{labelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteLabelAsync(Guid labelId, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new DeleteLabel(labelId), cancellationToken);
        return NoContent();
    }

    // ---------- lists ----------

    [HttpPost("boards/{boardId:guid}/lists")]
    [ProducesResponseType<ListDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateListAsync(Guid boardId, CreateListBody body, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await dispatcher.SendAsync(new CreateList(boardId, body.Title), cancellationToken));

    /// <summary>Archived columns of a board (restore one with <c>PATCH /api/lists/{id} { isArchived: false }</c>).</summary>
    [HttpGet("boards/{boardId:guid}/lists/archived")]
    public Task<IReadOnlyList<ArchivedListDto>> ListArchivedListsAsync(Guid boardId, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new ListArchivedLists(boardId), cancellationToken);

    [HttpPatch("lists/{listId:guid}")]
    public Task<ListDto> UpdateListAsync(Guid listId, UpdateListBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new UpdateList(listId, body.Title, body.IsArchived), cancellationToken);

    [HttpPost("lists/{listId:guid}/move")]
    public Task<ListDto> MoveListAsync(Guid listId, MoveBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new MoveList(listId, body.AfterId, body.BeforeId), cancellationToken);

    [HttpDelete("lists/{listId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteListAsync(Guid listId, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new DeleteList(listId), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateBoardBody(string? Title, string? Description, string? Color);

public sealed record UpdateBoardBody(Optional<string?> Title, Optional<string?> Description, Optional<string?> Color, Optional<bool?> IsArchived);

public sealed record CreateLabelBody(string? Name, string? Color);

public sealed record UpdateLabelBody(Optional<string?> Name, Optional<string?> Color);

public sealed record CreateListBody(string? Title);

public sealed record UpdateListBody(Optional<string?> Title, Optional<bool?> IsArchived);

/// <summary>Neighbours at the drop position: the item that should end up just before / just after the moved one.</summary>
public sealed record MoveBody(Guid? AfterId, Guid? BeforeId);
