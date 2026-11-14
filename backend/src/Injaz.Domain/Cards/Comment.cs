using Injaz.Domain.Common;

namespace Injaz.Domain.Cards;

/// <summary>A markdown comment on a card. Only its author may edit it.</summary>
public sealed class Comment : Entity
{
    public const int BodyMaxLength = 10_000;

    private Comment()
    {
        // EF Core
    }

    public Guid CardId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    public static Comment Create(Guid cardId, Guid authorId, string body, DateTimeOffset now) =>
        new() { CardId = cardId, AuthorId = authorId, Body = body.Trim(), CreatedAt = now };

    public void Edit(Guid editorId, string body, DateTimeOffset now)
    {
        if (editorId != AuthorId)
        {
            throw new DomainException("comment.notAuthor", "Only the author can edit a comment.");
        }

        Body = body.Trim();
        EditedAt = now;
    }
}
