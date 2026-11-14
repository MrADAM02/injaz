using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Activity;
using Injaz.Application.Features.Boards;
using Injaz.Application.Features.Checklist;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Realtime;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Activity;
using Injaz.Domain.Cards;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Cards;

/// <summary>The full card, for the card dialog. Comments and activity are paginated separately.</summary>
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
    IReadOnlyList<ChecklistItemDto> Checklist,
    int CommentCount,
    Guid CreatedById,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CardDto From(Card card, IReadOnlyList<ChecklistItemDto> checklist, int commentCount)
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
            checklist,
            commentCount,
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

    public static async Task<CardDto> ToDetailAsync(this IAppDbContext db, Card card, CancellationToken cancellationToken)
    {
        var checklist = await db.ChecklistItems
            .AsNoTracking()
            .Where(i => i.CardId == card.Id)
            .OrderBy(i => i.Rank)
            .ThenBy(i => i.Id)
            .Select(i => new ChecklistItemDto(i.Id, i.CardId, i.Text, i.IsDone, i.Rank))
            .ToListAsync(cancellationToken);
        var comments = await db.Comments.CountAsync(c => c.CardId == card.Id, cancellationToken);
        return CardDto.From(card, checklist, comments);
    }
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

internal sealed class CreateCardHandler(IAppDbContext db, ICurrentUser currentUser, IActivityLog activity, IBoardEvents events, TimeProvider clock)
    : ICommandHandler<CreateCard, CardSummaryDto>
{
    public async Task<CardSummaryDto> HandleAsync(CreateCard command, CancellationToken cancellationToken)
    {
        var list = await db.BoardLists.Where(l => l.Id == command.ListId).Select(l => new { l.BoardId, l.Title }).SingleAsync(cancellationToken);
        var siblings = db.Cards.Where(c => c.ListId == command.ListId);

        Guid? beforeId = null;
        if (command.Position == CardPosition.Top)
        {
            beforeId = await siblings.OrderBy(c => c.Rank).ThenBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
        }

        var rank = await Placement.RankAsync(
            siblings, movingId: null, afterId: null, beforeId, cancellationToken, reranked: c => events.CardChanged(c.BoardId, c.Id));
        var card = Card.Create(list.BoardId, command.ListId, command.Title!, rank, currentUser.UserId, clock.GetUtcNow());
        db.Cards.Add(card);
        events.CardChanged(card.BoardId, card.Id);
        activity.Record(ActivityTypes.CardCreated, card.BoardId, card.Id, new { title = card.Title, listId = card.ListId, list = list.Title });
        return CardSummaryDto.From(card, CardBadges.None);
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

        return await db.ToDetailAsync(card, cancellationToken);
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

/// <summary>Applies the patch, then logs one activity entry per field that actually changed.</summary>
internal sealed class UpdateCardHandler(IAppDbContext db, IActivityLog activity, IBoardEvents events, TimeProvider clock)
    : ICommandHandler<UpdateCard, CardDto>
{
    public async Task<CardDto> HandleAsync(UpdateCard command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        events.CardChanged(card.BoardId, card.Id); // archived → the flush turns it into card.removed
        var now = clock.GetUtcNow();
        var before = (card.Title, card.Description, card.DueAt, card.Priority, card.IsCompleted, card.IsArchived);

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

        void Log(string type, object data) => activity.Record(type, card.BoardId, card.Id, data);

        if (card.Title != before.Title)
        {
            Log(ActivityTypes.CardRenamed, new { from = before.Title, to = card.Title });
        }

        if (card.Description != before.Description)
        {
            Log(ActivityTypes.CardDescriptionChanged, new { title = card.Title });
        }

        if (card.DueAt != before.DueAt)
        {
            Log(ActivityTypes.CardDueChanged, new { title = card.Title, from = before.DueAt, to = card.DueAt });
        }

        if (card.Priority != before.Priority)
        {
            Log(ActivityTypes.CardPriorityChanged, new { title = card.Title, from = before.Priority, to = card.Priority });
        }

        if (card.IsCompleted != before.IsCompleted)
        {
            Log(card.IsCompleted ? ActivityTypes.CardCompleted : ActivityTypes.CardReopened, new { title = card.Title });
        }

        if (card.IsArchived != before.IsArchived)
        {
            Log(card.IsArchived ? ActivityTypes.CardArchived : ActivityTypes.CardRestored, new { title = card.Title });
        }

        return await db.ToDetailAsync(card, cancellationToken);
    }
}

// ---------- move (Member+) ----------

/// <summary>
/// Moves a card to <paramref name="ListId"/> (default: its current list) between two neighbouring cards in that
/// list. Send the ids of the cards the client sees above/below the drop position; either may be omitted.
/// Only moves to another list are logged — reordering within a list is too noisy for the activity feed.
/// </summary>
public sealed record MoveCard(Guid CardId, Guid? ListId, Guid? AfterId, Guid? BeforeId) : ICommand<CardSummaryDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class MoveCardHandler(IAppDbContext db, IActivityLog activity, IBoardEvents events, TimeProvider clock)
    : ICommandHandler<MoveCard, CardSummaryDto>
{
    public async Task<CardSummaryDto> HandleAsync(MoveCard command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        events.CardChanged(card.BoardId, card.Id);
        var fromListId = card.ListId;
        var targetListId = command.ListId ?? fromListId;

        if (targetListId != fromListId)
        {
            // The scope check covered the card; the target list must be on the same board.
            var sameBoard = await db.BoardLists.AnyAsync(l => l.Id == targetListId && l.BoardId == card.BoardId, cancellationToken);
            if (!sameBoard)
            {
                throw new NotFoundException("list");
            }
        }

        var rank = await Placement.RankAsync(
            db.Cards.Where(c => c.ListId == targetListId), card.Id, command.AfterId, command.BeforeId, cancellationToken,
            reranked: c => events.CardChanged(c.BoardId, c.Id));
        card.MoveTo(targetListId, rank, clock.GetUtcNow());

        if (targetListId != fromListId)
        {
            var titles = await db.BoardLists
                .Where(l => l.Id == fromListId || l.Id == targetListId)
                .ToDictionaryAsync(l => l.Id, l => l.Title, cancellationToken);
            activity.Record(ActivityTypes.CardMoved, card.BoardId, card.Id, new
            {
                title = card.Title,
                fromListId,
                fromList = titles[fromListId],
                toListId = targetListId,
                toList = titles[targetListId],
            });
        }

        return CardSummaryDto.From(card, await db.BadgesAsync(card.Id, cancellationToken));
    }
}

// ---------- delete (Admin+); members archive instead ----------

public sealed record DeleteCard(Guid CardId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.DeleteContent;
}

internal sealed class DeleteCardHandler(IAppDbContext db, IActivityLog activity, IBoardEvents events) : ICommandHandler<DeleteCard, Unit>
{
    public async Task<Unit> HandleAsync(DeleteCard command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        db.Cards.Remove(card);
        events.CardDeleted(card.BoardId, card.Id);

        // Logged on the board without a card reference: the card (and its own feed) is gone.
        activity.Record(ActivityTypes.CardDeleted, card.BoardId, cardId: null, new { cardId = card.Id, title = card.Title });
        return Unit.Value;
    }
}
