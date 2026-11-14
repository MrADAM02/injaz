using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Activity;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Domain.Activity;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Cards;

// All four are idempotent (PUT / DELETE semantics) and need EditContent on the card's workspace.
// Activity is only recorded when something actually changed.

/// <summary>Assigns a workspace member to the card.</summary>
public sealed record AssignCard(Guid CardId, Guid UserId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class AssignCardHandler(IAppDbContext db, ICurrentAccess currentAccess, IActivityLog activity, TimeProvider clock)
    : ICommandHandler<AssignCard, Unit>
{
    public async Task<Unit> HandleAsync(AssignCard command, CancellationToken cancellationToken)
    {
        var member = await db.WorkspaceMembers
            .Where(m => m.WorkspaceId == currentAccess.Access.WorkspaceId && m.UserId == command.UserId)
            .Join(db.Users, m => m.UserId, u => u.Id, (_, u) => new { u.Id, u.DisplayName })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("member");

        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        if (card.Assign(command.UserId, clock.GetUtcNow()))
        {
            activity.Record(ActivityTypes.CardAssigned, card.BoardId, card.Id, new { title = card.Title, userId = member.Id, name = member.DisplayName });
        }

        return Unit.Value;
    }
}

public sealed record UnassignCard(Guid CardId, Guid UserId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class UnassignCardHandler(IAppDbContext db, IActivityLog activity, TimeProvider clock) : ICommandHandler<UnassignCard, Unit>
{
    public async Task<Unit> HandleAsync(UnassignCard command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        if (card.Unassign(command.UserId, clock.GetUtcNow()))
        {
            var name = await db.Users.Where(u => u.Id == command.UserId).Select(u => u.DisplayName).SingleOrDefaultAsync(cancellationToken);
            activity.Record(ActivityTypes.CardUnassigned, card.BoardId, card.Id, new { title = card.Title, userId = command.UserId, name });
        }

        return Unit.Value;
    }
}

/// <summary>Attaches one of the board's labels to the card.</summary>
public sealed record AddCardLabel(Guid CardId, Guid LabelId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class AddCardLabelHandler(IAppDbContext db, IActivityLog activity, TimeProvider clock) : ICommandHandler<AddCardLabel, Unit>
{
    public async Task<Unit> HandleAsync(AddCardLabel command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);

        // Labels are board-scoped: a label id from another board (even one the caller can see) is not valid here.
        var label = await db.Labels.AsNoTracking().SingleOrDefaultAsync(l => l.Id == command.LabelId && l.BoardId == card.BoardId, cancellationToken)
            ?? throw new NotFoundException("label");

        if (card.AddLabel(label.Id, clock.GetUtcNow()))
        {
            activity.Record(ActivityTypes.CardLabelAdded, card.BoardId, card.Id, new { title = card.Title, labelId = label.Id, name = label.Name, color = label.Color });
        }

        return Unit.Value;
    }
}

public sealed record RemoveCardLabel(Guid CardId, Guid LabelId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class RemoveCardLabelHandler(IAppDbContext db, IActivityLog activity, TimeProvider clock) : ICommandHandler<RemoveCardLabel, Unit>
{
    public async Task<Unit> HandleAsync(RemoveCardLabel command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        if (card.RemoveLabel(command.LabelId, clock.GetUtcNow()))
        {
            var label = await db.Labels.AsNoTracking().Where(l => l.Id == command.LabelId).Select(l => new { l.Name, l.Color }).SingleOrDefaultAsync(cancellationToken);
            activity.Record(ActivityTypes.CardLabelRemoved, card.BoardId, card.Id, new { title = card.Title, labelId = command.LabelId, name = label?.Name, color = label?.Color });
        }

        return Unit.Value;
    }
}
