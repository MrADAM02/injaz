using Injaz.Domain.Common;
using Injaz.Domain.Ordering;

namespace Injaz.Domain.Boards;

/// <summary>A column on a board. Ordered within the board by <see cref="Rank"/>.</summary>
public sealed class BoardList : Entity, IRanked
{
    public const int TitleMaxLength = 200;

    private BoardList()
    {
        // EF Core
    }

    public Guid BoardId { get; private set; }

    public string Title { get; private set; } = null!;

    public string Rank { get; private set; } = null!;

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static BoardList Create(Guid boardId, string title, string rank, DateTimeOffset now) =>
        new()
        {
            BoardId = boardId,
            Title = title.Trim(),
            Rank = rank,
            CreatedAt = now,
        };

    public void Rename(string title) => Title = title.Trim();

    public void SetArchived(bool archived) => IsArchived = archived;

    public void MoveTo(string rank) => Rank = rank;

    public void Rerank(string rank) => Rank = rank;
}
