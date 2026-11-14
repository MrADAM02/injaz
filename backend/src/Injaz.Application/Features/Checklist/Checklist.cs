using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Activity;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Activity;
using Injaz.Domain.Cards;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Checklist;

public sealed record ChecklistItemDto(Guid Id, Guid CardId, string Text, bool IsDone, string Rank)
{
    public static ChecklistItemDto From(ChecklistItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new(item.Id, item.CardId, item.Text, item.IsDone, item.Rank);
    }
}

internal static class ChecklistRules
{
    public static void Text(ValidationContext context, string field, string? text) =>
        context.RuleFor(field, text).NotEmpty().MaxLength(ChecklistItem.TextMaxLength);

    /// <summary>The item plus the board it lives on (for activity).</summary>
    public static async Task<(ChecklistItem Item, Guid BoardId)> LoadItemAsync(this IAppDbContext db, Guid itemId, CancellationToken cancellationToken)
    {
        var row = await db.ChecklistItems
            .Where(i => i.Id == itemId)
            .Join(db.Cards, i => i.CardId, c => c.Id, (i, c) => new { Item = i, c.BoardId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("checklistItem");
        return (row.Item, row.BoardId);
    }
}

// ---------- add (Member+): appended at the end ----------

public sealed record AddChecklistItem(Guid CardId, string? Text) : ICommand<ChecklistItemDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class AddChecklistItemValidator : IValidator<AddChecklistItem>
{
    public void Validate(AddChecklistItem request, ValidationContext context) =>
        ChecklistRules.Text(context, nameof(request.Text), request.Text);
}

internal sealed class AddChecklistItemHandler(IAppDbContext db, IActivityLog activity, TimeProvider clock)
    : ICommandHandler<AddChecklistItem, ChecklistItemDto>
{
    public async Task<ChecklistItemDto> HandleAsync(AddChecklistItem command, CancellationToken cancellationToken)
    {
        var boardId = await db.Cards.Where(c => c.Id == command.CardId).Select(c => c.BoardId).SingleAsync(cancellationToken);
        var rank = await Placement.RankAsync(
            db.ChecklistItems.Where(i => i.CardId == command.CardId), movingId: null, afterId: null, beforeId: null, cancellationToken);

        var item = ChecklistItem.Create(command.CardId, command.Text!, rank, clock.GetUtcNow());
        db.ChecklistItems.Add(item);
        activity.Record(ActivityTypes.ChecklistItemAdded, boardId, command.CardId, new { itemId = item.Id, text = item.Text });
        return ChecklistItemDto.From(item);
    }
}

// ---------- update: text and/or done (Member+) ----------

public sealed record UpdateChecklistItem(Guid ItemId, Optional<string?> Text, Optional<bool?> IsDone)
    : ICommand<ChecklistItemDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.ChecklistItem(ItemId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class UpdateChecklistItemValidator : IValidator<UpdateChecklistItem>
{
    public void Validate(UpdateChecklistItem request, ValidationContext context)
    {
        if (request.Text.HasValue)
        {
            ChecklistRules.Text(context, nameof(request.Text), request.Text.Value);
        }

        if (request.IsDone.HasValue)
        {
            context.RuleFor(nameof(request.IsDone), request.IsDone.Value).NotNull();
        }
    }
}

internal sealed class UpdateChecklistItemHandler(IAppDbContext db, IActivityLog activity)
    : ICommandHandler<UpdateChecklistItem, ChecklistItemDto>
{
    public async Task<ChecklistItemDto> HandleAsync(UpdateChecklistItem command, CancellationToken cancellationToken)
    {
        var (item, boardId) = await db.LoadItemAsync(command.ItemId, cancellationToken);

        if (command.Text.HasValue)
        {
            item.Rewrite(command.Text.Value!);
        }

        if (command.IsDone.HasValue && item.SetDone(command.IsDone.Value!.Value))
        {
            activity.Record(
                item.IsDone ? ActivityTypes.ChecklistItemCompleted : ActivityTypes.ChecklistItemReopened,
                boardId,
                item.CardId,
                new { itemId = item.Id, text = item.Text });
        }

        return ChecklistItemDto.From(item);
    }
}

// ---------- move (Member+) ----------

public sealed record MoveChecklistItem(Guid ItemId, Guid? AfterId, Guid? BeforeId) : ICommand<ChecklistItemDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.ChecklistItem(ItemId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class MoveChecklistItemHandler(IAppDbContext db) : ICommandHandler<MoveChecklistItem, ChecklistItemDto>
{
    public async Task<ChecklistItemDto> HandleAsync(MoveChecklistItem command, CancellationToken cancellationToken)
    {
        var (item, _) = await db.LoadItemAsync(command.ItemId, cancellationToken);
        var rank = await Placement.RankAsync(
            db.ChecklistItems.Where(i => i.CardId == item.CardId), item.Id, command.AfterId, command.BeforeId, cancellationToken);

        item.MoveTo(rank);
        return ChecklistItemDto.From(item);
    }
}

// ---------- delete (Member+): checklist items are lightweight, unlike cards ----------

public sealed record DeleteChecklistItem(Guid ItemId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.ChecklistItem(ItemId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class DeleteChecklistItemHandler(IAppDbContext db, IActivityLog activity) : ICommandHandler<DeleteChecklistItem, Unit>
{
    public async Task<Unit> HandleAsync(DeleteChecklistItem command, CancellationToken cancellationToken)
    {
        var (item, boardId) = await db.LoadItemAsync(command.ItemId, cancellationToken);
        db.ChecklistItems.Remove(item);
        activity.Record(ActivityTypes.ChecklistItemDeleted, boardId, item.CardId, new { itemId = item.Id, text = item.Text });
        return Unit.Value;
    }
}
