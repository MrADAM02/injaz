using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Activity;
using Injaz.Application.Features.Cards;
using Injaz.Domain.Activity;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Realtime;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Boards;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Boards;

internal static class BoardRules
{
    public static void Title(ValidationContext context, string field, string? title) =>
        context.RuleFor(field, title).NotEmpty().MaxLength(Board.TitleMaxLength);

    public static void Color(ValidationContext context, string field, string? color) =>
        context.RuleFor(field, color).Must(Palette.IsValid, ValidationCodes.InvalidValue);

    public static async Task<Board> LoadBoardAsync(this IAppDbContext db, Guid boardId, CancellationToken cancellationToken) =>
        await db.Boards.SingleOrDefaultAsync(b => b.Id == boardId, cancellationToken)
        ?? throw new NotFoundException("board");
}

// ---------- create (Member+) ----------

public sealed record CreateBoard(Guid WorkspaceId, string? Title, string? Description, string? Color)
    : ICommand<BoardSummaryDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class CreateBoardValidator : IValidator<CreateBoard>
{
    public void Validate(CreateBoard request, ValidationContext context)
    {
        BoardRules.Title(context, nameof(request.Title), request.Title);
        context.RuleFor(nameof(request.Description), request.Description).MaxLength(Board.DescriptionMaxLength);
        if (request.Color is not null)
        {
            BoardRules.Color(context, nameof(request.Color), request.Color);
        }
    }
}

internal sealed class CreateBoardHandler(IAppDbContext db, ICurrentUser currentUser, IActivityLog activity, TimeProvider clock)
    : ICommandHandler<CreateBoard, BoardSummaryDto>
{
    public Task<BoardSummaryDto> HandleAsync(CreateBoard command, CancellationToken cancellationToken)
    {
        var board = Board.Create(command.WorkspaceId, command.Title!, command.Description, command.Color, currentUser.UserId, clock.GetUtcNow());
        db.Boards.Add(board);
        activity.Record(ActivityTypes.BoardCreated, board.Id, data: new { title = board.Title });
        return Task.FromResult(BoardSummaryDto.From(board));
    }
}

// ---------- list (any member) ----------

/// <summary>Boards in a workspace, alphabetically. Bounded per workspace, so not paginated.</summary>
public sealed record ListBoards(Guid WorkspaceId, bool IncludeArchived = false)
    : IQuery<IReadOnlyList<BoardSummaryDto>>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.View;
}

internal sealed class ListBoardsHandler(IAppDbContext db) : IQueryHandler<ListBoards, IReadOnlyList<BoardSummaryDto>>
{
    public async Task<IReadOnlyList<BoardSummaryDto>> HandleAsync(ListBoards query, CancellationToken cancellationToken) =>
        await db.Boards
            .AsNoTracking()
            .Where(b => b.WorkspaceId == query.WorkspaceId && (query.IncludeArchived || !b.IsArchived))
            .OrderBy(b => b.Title)
            .ThenBy(b => b.Id)
            .Select(b => new BoardSummaryDto(b.Id, b.WorkspaceId, b.Title, b.Description, b.Color, b.IsArchived, b.CreatedAt))
            .ToListAsync(cancellationToken);
}

// ---------- get the whole board (any member) ----------

public sealed record GetBoard(Guid BoardId) : IQuery<BoardDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Board(BoardId);

    public Permission Permission => Permission.View;
}

internal sealed class GetBoardHandler(IAppDbContext db, ICurrentAccess currentAccess) : IQueryHandler<GetBoard, BoardDto>
{
    public async Task<BoardDto> HandleAsync(GetBoard query, CancellationToken cancellationToken)
    {
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == query.BoardId, cancellationToken)
            ?? throw new NotFoundException("board");

        var lists = await db.BoardLists
            .AsNoTracking()
            .Where(l => l.BoardId == board.Id && !l.IsArchived)
            .OrderBy(l => l.Rank)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        var cards = await db.Cards
            .AsNoTracking()
            .Where(c => c.BoardId == board.Id && !c.IsArchived)
            .OrderBy(c => c.Rank)
            .ThenBy(c => c.Id)
            .Select(CardQueries.Summary(db))
            .ToListAsync(cancellationToken);

        var labels = await db.Labels
            .AsNoTracking()
            .Where(l => l.BoardId == board.Id)
            .OrderBy(l => l.Color)
            .ThenBy(l => l.Name)
            .Select(l => new LabelDto(l.Id, l.Name, l.Color))
            .ToListAsync(cancellationToken);

        var members = await db.WorkspaceMembers
            .AsNoTracking()
            .Where(m => m.WorkspaceId == board.WorkspaceId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { Member = m, User = u })
            .OrderBy(x => x.User.DisplayName)
            .Select(x => new BoardMemberDto(x.User.Id, x.User.DisplayName, x.User.AvatarUrl, x.Member.Role))
            .ToListAsync(cancellationToken);

        // Cards were fetched in global rank order, so grouping keeps each list's cards in order.
        var cardsByList = cards.ToLookup(c => c.ListId);
        var listDtos = lists.Select(l => ListDto.From(l, [.. cardsByList[l.Id]])).ToList();

        return new BoardDto(BoardSummaryDto.From(board), currentAccess.Access.Role, listDtos, labels, members);
    }
}

// ---------- update / archive (Member+) ----------

public sealed record UpdateBoard(
    Guid BoardId,
    Optional<string?> Title,
    Optional<string?> Description,
    Optional<string?> Color,
    Optional<bool?> IsArchived) : ICommand<BoardSummaryDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Board(BoardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class UpdateBoardValidator : IValidator<UpdateBoard>
{
    public void Validate(UpdateBoard request, ValidationContext context)
    {
        if (request.Title.HasValue)
        {
            BoardRules.Title(context, nameof(request.Title), request.Title.Value);
        }

        if (request.Description.HasValue)
        {
            context.RuleFor(nameof(request.Description), request.Description.Value).MaxLength(Board.DescriptionMaxLength);
        }

        if (request.Color.HasValue)
        {
            BoardRules.Color(context, nameof(request.Color), request.Color.Value);
        }

        if (request.IsArchived.HasValue)
        {
            context.RuleFor(nameof(request.IsArchived), request.IsArchived.Value).NotNull();
        }
    }
}

internal sealed class UpdateBoardHandler(IAppDbContext db, IActivityLog activity, IBoardEvents events, TimeProvider clock)
    : ICommandHandler<UpdateBoard, BoardSummaryDto>
{
    public async Task<BoardSummaryDto> HandleAsync(UpdateBoard command, CancellationToken cancellationToken)
    {
        var board = await db.LoadBoardAsync(command.BoardId, cancellationToken);
        events.BoardChanged(board.Id);
        var now = clock.GetUtcNow();
        var (oldTitle, wasArchived) = (board.Title, board.IsArchived);

        if (command.Title.HasValue)
        {
            board.Rename(command.Title.Value!, now);
            if (board.Title != oldTitle)
            {
                activity.Record(ActivityTypes.BoardRenamed, board.Id, data: new { from = oldTitle, to = board.Title });
            }
        }

        if (command.Description.HasValue)
        {
            board.Describe(command.Description.Value, now);
        }

        if (command.Color.HasValue)
        {
            board.Recolor(command.Color.Value!, now);
        }

        if (command.IsArchived.HasValue)
        {
            board.SetArchived(command.IsArchived.Value!.Value, now);
            if (board.IsArchived != wasArchived)
            {
                activity.Record(board.IsArchived ? ActivityTypes.BoardArchived : ActivityTypes.BoardRestored, board.Id, data: new { title = board.Title });
            }
        }

        return BoardSummaryDto.From(board);
    }
}

// ---------- delete (Admin+) ----------

/// <summary>Permanently deletes the board with its lists, cards and labels. Members archive instead.</summary>
public sealed record DeleteBoard(Guid BoardId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Board(BoardId);

    public Permission Permission => Permission.DeleteContent;
}

internal sealed class DeleteBoardHandler(IAppDbContext db, IBoardEvents events) : ICommandHandler<DeleteBoard, Unit>
{
    public async Task<Unit> HandleAsync(DeleteBoard command, CancellationToken cancellationToken)
    {
        db.Boards.Remove(await db.LoadBoardAsync(command.BoardId, cancellationToken));
        events.BoardDeleted(command.BoardId);
        return Unit.Value;
    }
}
