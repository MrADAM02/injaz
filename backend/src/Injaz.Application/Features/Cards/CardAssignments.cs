using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Cards;

// All four are idempotent (PUT / DELETE semantics) and need EditContent on the card's workspace.

/// <summary>Assigns a workspace member to the card.</summary>
public sealed record AssignCard(Guid CardId, Guid UserId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class AssignCardHandler(IAppDbContext db, ICurrentAccess currentAccess, TimeProvider clock) : ICommandHandler<AssignCard, Unit>
{
    public async Task<Unit> HandleAsync(AssignCard command, CancellationToken cancellationToken)
    {
        var isMember = await db.WorkspaceMembers.AnyAsync(
            m => m.WorkspaceId == currentAccess.Access.WorkspaceId && m.UserId == command.UserId, cancellationToken);
        if (!isMember)
        {
            throw new NotFoundException("member");
        }

        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        card.Assign(command.UserId, clock.GetUtcNow());
        return Unit.Value;
    }
}

public sealed record UnassignCard(Guid CardId, Guid UserId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class UnassignCardHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<UnassignCard, Unit>
{
    public async Task<Unit> HandleAsync(UnassignCard command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        card.Unassign(command.UserId, clock.GetUtcNow());
        return Unit.Value;
    }
}

/// <summary>Attaches one of the board's labels to the card.</summary>
public sealed record AddCardLabel(Guid CardId, Guid LabelId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class AddCardLabelHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<AddCardLabel, Unit>
{
    public async Task<Unit> HandleAsync(AddCardLabel command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);

        // Labels are board-scoped: a label id from another board (even one the caller can see) is not valid here.
        var onSameBoard = await db.Labels.AnyAsync(l => l.Id == command.LabelId && l.BoardId == card.BoardId, cancellationToken);
        if (!onSameBoard)
        {
            throw new NotFoundException("label");
        }

        card.AddLabel(command.LabelId, clock.GetUtcNow());
        return Unit.Value;
    }
}

public sealed record RemoveCardLabel(Guid CardId, Guid LabelId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class RemoveCardLabelHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<RemoveCardLabel, Unit>
{
    public async Task<Unit> HandleAsync(RemoveCardLabel command, CancellationToken cancellationToken)
    {
        var card = await db.LoadCardAsync(command.CardId, cancellationToken);
        card.RemoveLabel(command.LabelId, clock.GetUtcNow());
        return Unit.Value;
    }
}
