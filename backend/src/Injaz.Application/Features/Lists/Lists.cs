using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Activity;
using Injaz.Application.Features.Boards;
using Injaz.Domain.Activity;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Boards;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Lists;

internal static class ListRules
{
    public static void Title(ValidationContext context, string field, string? title) =>
        context.RuleFor(field, title).NotEmpty().MaxLength(BoardList.TitleMaxLength);

    public static async Task<BoardList> LoadListAsync(this IAppDbContext db, Guid listId, CancellationToken cancellationToken) =>
        await db.BoardLists.SingleOrDefaultAsync(l => l.Id == listId, cancellationToken)
        ?? throw new NotFoundException("list");
}

// ---------- create (Member+): appended as the last column ----------

public sealed record CreateList(Guid BoardId, string? Title) : ICommand<ListDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Board(BoardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class CreateListValidator : IValidator<CreateList>
{
    public void Validate(CreateList request, ValidationContext context) =>
        ListRules.Title(context, nameof(request.Title), request.Title);
}

internal sealed class CreateListHandler(IAppDbContext db, IActivityLog activity, TimeProvider clock) : ICommandHandler<CreateList, ListDto>
{
    public async Task<ListDto> HandleAsync(CreateList command, CancellationToken cancellationToken)
    {
        var rank = await Placement.RankAsync(
            db.BoardLists.Where(l => l.BoardId == command.BoardId), movingId: null, afterId: null, beforeId: null, cancellationToken);

        var list = BoardList.Create(command.BoardId, command.Title!, rank, clock.GetUtcNow());
        db.BoardLists.Add(list);
        activity.Record(ActivityTypes.ListCreated, list.BoardId, data: new { listId = list.Id, title = list.Title });
        return ListDto.From(list);
    }
}

// ---------- update / archive (Member+) ----------

public sealed record UpdateList(Guid ListId, Optional<string?> Title, Optional<bool?> IsArchived) : ICommand<ListDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.List(ListId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class UpdateListValidator : IValidator<UpdateList>
{
    public void Validate(UpdateList request, ValidationContext context)
    {
        if (request.Title.HasValue)
        {
            ListRules.Title(context, nameof(request.Title), request.Title.Value);
        }

        if (request.IsArchived.HasValue)
        {
            context.RuleFor(nameof(request.IsArchived), request.IsArchived.Value).NotNull();
        }
    }
}

internal sealed class UpdateListHandler(IAppDbContext db, IActivityLog activity) : ICommandHandler<UpdateList, ListDto>
{
    public async Task<ListDto> HandleAsync(UpdateList command, CancellationToken cancellationToken)
    {
        var list = await db.LoadListAsync(command.ListId, cancellationToken);
        var (oldTitle, wasArchived) = (list.Title, list.IsArchived);

        if (command.Title.HasValue)
        {
            list.Rename(command.Title.Value!);
            if (list.Title != oldTitle)
            {
                activity.Record(ActivityTypes.ListRenamed, list.BoardId, data: new { listId = list.Id, from = oldTitle, to = list.Title });
            }
        }

        if (command.IsArchived.HasValue)
        {
            list.SetArchived(command.IsArchived.Value!.Value);
            if (list.IsArchived != wasArchived)
            {
                activity.Record(list.IsArchived ? ActivityTypes.ListArchived : ActivityTypes.ListRestored, list.BoardId, data: new { listId = list.Id, title = list.Title });
            }
        }

        return ListDto.From(list);
    }
}

// ---------- move (Member+) ----------

/// <summary>Repositions a column on its board between two neighbouring columns (either may be omitted).</summary>
public sealed record MoveList(Guid ListId, Guid? AfterId, Guid? BeforeId) : ICommand<ListDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.List(ListId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class MoveListHandler(IAppDbContext db) : ICommandHandler<MoveList, ListDto>
{
    public async Task<ListDto> HandleAsync(MoveList command, CancellationToken cancellationToken)
    {
        var list = await db.LoadListAsync(command.ListId, cancellationToken);

        var rank = await Placement.RankAsync(
            db.BoardLists.Where(l => l.BoardId == list.BoardId), list.Id, command.AfterId, command.BeforeId, cancellationToken);

        list.MoveTo(rank);
        return ListDto.From(list);
    }
}

// ---------- delete (Admin+): removes its cards too ----------

public sealed record DeleteList(Guid ListId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.List(ListId);

    public Permission Permission => Permission.DeleteContent;
}

internal sealed class DeleteListHandler(IAppDbContext db, IActivityLog activity) : ICommandHandler<DeleteList, Unit>
{
    public async Task<Unit> HandleAsync(DeleteList command, CancellationToken cancellationToken)
    {
        var list = await db.LoadListAsync(command.ListId, cancellationToken);
        db.BoardLists.Remove(list);
        activity.Record(ActivityTypes.ListDeleted, list.BoardId, data: new { listId = list.Id, title = list.Title });
        return Unit.Value;
    }
}
