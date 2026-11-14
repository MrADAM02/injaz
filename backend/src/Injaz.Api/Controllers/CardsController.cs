using Injaz.Application.Common;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Cards;
using Injaz.Application.Messaging;
using Injaz.Domain.Cards;
using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.Controllers;

[Route("api")]
public sealed class CardsController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpPost("lists/{listId:guid}/cards")]
    [ProducesResponseType<CardSummaryDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(Guid listId, CreateCardBody body, CancellationToken cancellationToken)
    {
        var card = await dispatcher.SendAsync(new CreateCard(listId, body.Title, body.Position), cancellationToken);
        return Created($"/api/cards/{card.Id}", card);
    }

    [HttpGet("cards/{cardId:guid}")]
    public Task<CardDto> GetAsync(Guid cardId, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new GetCard(cardId), cancellationToken);

    /// <summary>Merge-patch: omitted fields are unchanged; <c>"dueAt": null</c> clears the due date.</summary>
    [HttpPatch("cards/{cardId:guid}")]
    public Task<CardDto> UpdateAsync(Guid cardId, UpdateCardBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(
            new UpdateCard(cardId, body.Title, body.Description, body.DueAt, body.Priority, body.IsCompleted, body.IsArchived),
            cancellationToken);

    [HttpPost("cards/{cardId:guid}/move")]
    public Task<CardSummaryDto> MoveAsync(Guid cardId, MoveCardBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new MoveCard(cardId, body.ListId, body.AfterId, body.BeforeId), cancellationToken);

    [HttpDelete("cards/{cardId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid cardId, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new DeleteCard(cardId), cancellationToken);
        return NoContent();
    }

    // ---------- assignees & labels (idempotent) ----------

    [HttpPut("cards/{cardId:guid}/assignees/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> AssignAsync(Guid cardId, Guid userId, CancellationToken cancellationToken) =>
        NoContentAfter(dispatcher.SendAsync(new AssignCard(cardId, userId), cancellationToken));

    [HttpDelete("cards/{cardId:guid}/assignees/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> UnassignAsync(Guid cardId, Guid userId, CancellationToken cancellationToken) =>
        NoContentAfter(dispatcher.SendAsync(new UnassignCard(cardId, userId), cancellationToken));

    [HttpPut("cards/{cardId:guid}/labels/{labelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> AddLabelAsync(Guid cardId, Guid labelId, CancellationToken cancellationToken) =>
        NoContentAfter(dispatcher.SendAsync(new AddCardLabel(cardId, labelId), cancellationToken));

    [HttpDelete("cards/{cardId:guid}/labels/{labelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> RemoveLabelAsync(Guid cardId, Guid labelId, CancellationToken cancellationToken) =>
        NoContentAfter(dispatcher.SendAsync(new RemoveCardLabel(cardId, labelId), cancellationToken));

    private async Task<IActionResult> NoContentAfter(Task<Unit> command)
    {
        await command;
        return NoContent();
    }
}

public sealed record CreateCardBody(string? Title, CardPosition? Position);

public sealed record UpdateCardBody(
    Optional<string?> Title,
    Optional<string?> Description,
    Optional<DateTimeOffset?> DueAt,
    Optional<CardPriority?> Priority,
    Optional<bool?> IsCompleted,
    Optional<bool?> IsArchived);

/// <summary><c>listId</c> defaults to the card's current list.</summary>
public sealed record MoveCardBody(Guid? ListId, Guid? AfterId, Guid? BeforeId);
