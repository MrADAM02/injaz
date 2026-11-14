using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Activity;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Realtime;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Activity;
using Injaz.Domain.Cards;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Comments;

public sealed record CommentDto(Guid Id, Guid CardId, ActorDto Author, string Body, DateTimeOffset CreatedAt, DateTimeOffset? EditedAt);

internal static class CommentRules
{
    public const int ExcerptLength = 140;

    public static void Body(ValidationContext context, string? body) =>
        context.RuleFor("body", body).NotEmpty().MaxLength(Comment.BodyMaxLength);

    public static async Task<Comment> LoadCommentAsync(this IAppDbContext db, Guid commentId, CancellationToken cancellationToken) =>
        await db.Comments.SingleOrDefaultAsync(c => c.Id == commentId, cancellationToken)
        ?? throw new NotFoundException("comment");

    public static async Task<CommentDto> ToDtoAsync(this IAppDbContext db, Comment comment, CancellationToken cancellationToken)
    {
        var author = await db.Users.AsNoTracking()
            .Where(u => u.Id == comment.AuthorId)
            .Select(u => new ActorDto(u.Id, u.DisplayName, u.AvatarUrl))
            .SingleAsync(cancellationToken);
        return new CommentDto(comment.Id, comment.CardId, author, comment.Body, comment.CreatedAt, comment.EditedAt);
    }
}

// ---------- list (any member): newest first, paginated ----------

public sealed record ListComments(Guid CardId, string? Cursor, int? Limit) : IQuery<PagedResult<CommentDto>>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.View;
}

internal sealed class ListCommentsValidator : IValidator<ListComments>
{
    public void Validate(ListComments request, ValidationContext context) => PageRules.Limit(context, request.Limit);
}

internal sealed class ListCommentsHandler(IAppDbContext db) : IQueryHandler<ListComments, PagedResult<CommentDto>>
{
    public async Task<PagedResult<CommentDto>> HandleAsync(ListComments query, CancellationToken cancellationToken)
    {
        var take = query.Limit ?? PageRules.DefaultLimit;
        var comments = db.Comments.AsNoTracking().Where(c => c.CardId == query.CardId);

        if (query.Cursor is not null)
        {
            var (at, id) = KeysetCursor.Decode(query.Cursor);
            comments = comments.Where(c => c.CreatedAt < at || (c.CreatedAt == at && c.Id < id));
        }

        var rows = await comments
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(take + 1)
            .Join(db.Users, c => c.AuthorId, u => u.Id, (c, u) => new CommentDto(
                c.Id, c.CardId, new ActorDto(u.Id, u.DisplayName, u.AvatarUrl), c.Body, c.CreatedAt, c.EditedAt))
            .ToListAsync(cancellationToken);

        rows = [.. rows.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)];
        return PagedResult<CommentDto>.From(rows, take, c => c, c => new KeysetCursor(c.CreatedAt, c.Id));
    }
}

// ---------- add (Member+) ----------

public sealed record AddComment(Guid CardId, string? Body) : ICommand<CommentDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Card(CardId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class AddCommentValidator : IValidator<AddComment>
{
    public void Validate(AddComment request, ValidationContext context) => CommentRules.Body(context, request.Body);
}

internal sealed class AddCommentHandler(IAppDbContext db, ICurrentUser currentUser, IActivityLog activity, IBoardEvents events, TimeProvider clock)
    : ICommandHandler<AddComment, CommentDto>
{
    public async Task<CommentDto> HandleAsync(AddComment command, CancellationToken cancellationToken)
    {
        var boardId = await db.Cards.Where(c => c.Id == command.CardId).Select(c => c.BoardId).SingleAsync(cancellationToken);

        var comment = Comment.Create(command.CardId, currentUser.UserId, command.Body!, clock.GetUtcNow());
        db.Comments.Add(comment);
        events.CardChanged(boardId, command.CardId);

        var excerpt = comment.Body.Length <= CommentRules.ExcerptLength ? comment.Body : comment.Body[..CommentRules.ExcerptLength] + "…";
        activity.Record(ActivityTypes.CommentAdded, boardId, command.CardId, new { commentId = comment.Id, excerpt });

        return await db.ToDtoAsync(comment, cancellationToken);
    }
}

// ---------- edit (author only) ----------

public sealed record EditComment(Guid CommentId, string? Body) : ICommand<CommentDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Comment(CommentId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class EditCommentValidator : IValidator<EditComment>
{
    public void Validate(EditComment request, ValidationContext context) => CommentRules.Body(context, request.Body);
}

internal sealed class EditCommentHandler(IAppDbContext db, ICurrentUser currentUser, IBoardEvents events, TimeProvider clock)
    : ICommandHandler<EditComment, CommentDto>
{
    public async Task<CommentDto> HandleAsync(EditComment command, CancellationToken cancellationToken)
    {
        var comment = await db.LoadCommentAsync(command.CommentId, cancellationToken);
        if (comment.AuthorId != currentUser.UserId)
        {
            throw new ForbiddenException("comment.notAuthor", "Only the author can edit a comment.");
        }

        comment.Edit(currentUser.UserId, command.Body!, clock.GetUtcNow());
        await events.CommentCardChangedAsync(db, comment, cancellationToken);
        return await db.ToDtoAsync(comment, cancellationToken);
    }
}

// ---------- delete (author, or Admin+ for anyone's) ----------

/// <summary>Declared at <see cref="Permission.EditContent"/>; deleting someone else's comment additionally needs DeleteContent.</summary>
public sealed record DeleteComment(Guid CommentId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Comment(CommentId);

    public Permission Permission => Permission.EditContent;
}

internal sealed class DeleteCommentHandler(IAppDbContext db, ICurrentAccess currentAccess, IBoardEvents events) : ICommandHandler<DeleteComment, Unit>
{
    public async Task<Unit> HandleAsync(DeleteComment command, CancellationToken cancellationToken)
    {
        var comment = await db.LoadCommentAsync(command.CommentId, cancellationToken);
        var access = currentAccess.Access;

        if (comment.AuthorId != access.UserId && !access.Role.Allows(Permission.DeleteContent))
        {
            throw new ForbiddenException();
        }

        db.Comments.Remove(comment);
        await events.CommentCardChangedAsync(db, comment, cancellationToken);
        return Unit.Value;
    }
}

internal static class CommentEvents
{
    public static async Task CommentCardChangedAsync(this IBoardEvents events, IAppDbContext db, Comment comment, CancellationToken cancellationToken)
    {
        var boardId = await db.Cards.Where(c => c.Id == comment.CardId).Select(c => c.BoardId).SingleAsync(cancellationToken);
        events.CardChanged(boardId, comment.CardId);
    }
}
