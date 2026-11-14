using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Boards;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Cards;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Cards;

/// <summary>The full card, for the card dialog.</summary>
public sealed record CardDto(
    Guid Id,
    Guid BoardId,
    Guid ListId,
    string Title,
    string? Description,
    string Rank,
    DateTimeOffset? DueAt,
    CardPriority Priority,
    DateTimeOffset? CompletedAt,
    bool IsArchived,
    IReadOnlyList<Guid> LabelIds,
    IReadOnlyList<Guid> AssigneeIds,
    Guid CreatedById,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CardDto From(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new(
            card.Id,
            card.BoardId,
            card.ListId,
            card.Title,
            card.Description,
            card.Rank,
            card.DueAt,
            card.Priority,
            card.CompletedAt,
            card.IsArchived,
            [.. card.Labels.Select(l => l.LabelId)],
            [.. card.Assignees.Select(a => a.UserId)],
            card.CreatedById,
            card.CreatedAt,
            card.UpdatedAt);
    }
}

public enum CardPosition
{
    Bottom = 0,
    Top = 1,
}

internal static class CardRules
{
    public static void Title(ValidationContext context, string field, string? title) =>
        context.RuleFor(field, title).NotEmpty().MaxLength(Card.TitleMaxLength);

    public static async Task<Card> LoadCardAsync(this IAppDbContext db, Guid cardId, CancellationToken cancellationToken) =>
        await db.Cards
            .Include(c => c.Assignees)
            .Include(c => c.Labels)
            .SingleOrDefaultAsync(c => c.Id == cardId, cancellationToken)
        ?? throw new NotFoundException("card");
}

// ---------- create (Member+) ----------

public sealed record CreateCard(Guid ListId, string? Title, CardPosition? Position) : ICommand<CardSummaryDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.List(ListId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class CreateCardValidator : IValidator<CreateCard>
{
    public void Validate(CreateCard request, ValidationContext context)
    {
        CardRules.Title(context, nameof(request.Title), request.Title);
        context.RuleFor(nameof(request.Position), request.Position).IsInEnum();
    }
}

internal sealed class CreateCardHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<CreateCard, CardSummaryDto>
{
    public async Task<CardSummaryDto> HandleAsync(CreateCard command, CancellationToken cancellationToken)
    {
        var boardId = await db.BoardLists.Where(l => l.Id == command.ListId).Select(l => l.BoardId).SingleAsync(cancellationToken);
        var siblings = db.Cards.Where(c => c.ListId == command.ListId);

        Guid? beforeId = null;
        if (command.Position == CardPosition.Top)
        {
            beforeId = await siblings.OrderBy(c => c.Rank).ThenBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
        }

        var rank = await Placement.RankAsync(siblings, movingId: null, afterId: null, beforeId, cancellationToken);
        var card = Card.Create(boardId, command.ListId, command.Title!, rank, currentUser.UserId, clock.GetUtcNow());
        db.Cards.Add(card);
        return CardSummaryDto.From(card);
    }
}

// ---------- get (any member) ----------

public sealed record GetCard(Guid CardId) : IQuery<CardDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.View;
}

internal sealed class GetCardHandler(IAppDbContext db) : IQueryHandler<GetCard, CardDto>
{
    public async Task<CardDto> HandleAsync(GetCard query, CancellationToken cancellationToken)
    {
        var card = await db.Cards
            .AsNoTracking()
            .Include(c => c.Assignees)
            .Include(c => c.Labels)
            .SingleOrDefaultAsync(c => c.Id == query.CardId, cancellationToken)
            ?? throw new NotFoundException("card");

        return CardDto.From(card);
    }
}

// ---------- update (Member+): JSON merge-patch semantics ----------

/// <summary>Omitted fields are unchanged; <c>"dueAt": null</c> clears the due date, <c>"description": null</c> clears the description.</summary>
public sealed record UpdateCard(
    Guid CardId,
    Optional<string?> Title,
    Optional<string?> Description,
    Optional<DateTimeOffset?> DueAt,
    Optional<CardPriority?> Priority,
    Optional<bool?> IsCompleted,
    Optional<bool?> IsArchived) : ICommand<CardDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class UpdateCardValidator : IValidator<UpdateCard>
{
    public void Validate(UpdateCard request, ValidationContext context)
    {
        if (request.Title.HasValue)
        {
            CardRules.Title(context, nameof(request.Title), request.Title.Value);
        }

        if (request.Description.HasValue)
        {
            context.RuleFor(nameof(request.Description), request.Description.Value).MaxLength(Card.DescriptionMaxLength);
        }

        if (request.Priority.HasValue)
        {
            context.RuleFor(nameof(request.Priority), request.Priority.Value).NotNull().IsInEnum();
        }

        if (request.IsCompleted.HasValue)
        {
            context.RuleFor(nameof(request.IsCompleted), request.IsCompleted.Value).NotNull();
        }

        if (request.IsArchived.HasValue)
        {
            context.RuleFor(nameof(request.IsArchived), request.IsArchived.Value).NotNull();
        }
    }
}

internal sealed class UpdateCardHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<UpdateCard, CardDto>
{
    public async Task<CardDto> HandleAsync(UpdateCard command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        var now = clock.GetUtcNow();

        if (command.Title.HasValue)
        {
            card.Rename(command.Title.Value!, now);
        }

        if (command.Description.HasValue)
        {
            card.Describe(command.Description.Value, now);
        }

        if (command.DueAt.HasValue)
        {
            card.Schedule(command.DueAt.Value, now);
        }

        if (command.Priority.HasValue)
        {
            card.Prioritize(command.Priority.Value!.Value, now);
        }

        if (command.IsCompleted.HasValue)
        {
            card.SetCompleted(command.IsCompleted.Value!.Value, now);
        }

        if (command.IsArchived.HasValue)
        {
            card.SetArchived(command.IsArchived.Value!.Value, now);
        }

        return CardDto.From(card);
    }
}

// ---------- move (Member+) ----------

/// <summary>
/// Moves a card to <paramref name="ListId"/> (default: its current list) between two neighbouring cards in that
/// list. Send the ids of the cards the client sees above/below the drop position; either may be omitted.
/// </summary>
public sealed record MoveCard(Guid CardId, Guid? ListId, Guid? AfterId, Guid? BeforeId) : ICommand<CardSummaryDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class MoveCardHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<MoveCard, CardSummaryDto>
{
    public async Task<CardSummaryDto> HandleAsync(MoveCard command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        var targetListId = command.ListId ?? card.ListId;

        if (targetListId != card.ListId)
        {
            // The scope check covered the card; the target list must be on the same board.
            var sameBoard = await db.BoardLists.AnyAsync(l => l.Id == targetListId && l.BoardId == card.BoardId, cancellationToken);
            if (!sameBoard)
            {
                throw new NotFoundException("list");
            }
        }

        var rank = await Placement.RankAsync(
            db.Cards.Where(c => c.ListId == targetListId), card.Id, command.AfterId, command.BeforeId, cancellationToken);

        card.MoveTo(targetListId, rank, clock.GetUtcNow());
        return CardSummaryDto.From(card);
    }
}

// ---------- delete (Admin+); members archive instead ----------

public sealed record DeleteCard(Guid CardId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.DeleteContent;
}

internal sealed class DeleteCardHandler(IAppDbContext db) : ICommandHandler<DeleteCard, Unit>
{
    public async Task<Unit> HandleAsync(DeleteCard command, CancellationToken cancellationToken)
    {
        db.Cards.Remove(await db.LoadCardAsync(command.CardId, cancellationToken));
        return Unit.Value;
    }
}
