using Injaz.Domain.Common;
using Injaz.Domain.Ordering;

namespace Injaz.Domain.Cards;

/// <summary>One item of a card's checklist (one checklist per card in the MVP). Ordered by <see cref="Rank"/>.</summary>
public sealed class ChecklistItem : Entity, IRanked
{
    public const int TextMaxLength = 500;

    private ChecklistItem()
    {
        // EF Core
    }

    public Guid CardId { get; private set; }

    public string Text { get; private set; } = null!;

    public bool IsDone { get; private set; }

    public string Rank { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public static ChecklistItem Create(Guid cardId, string text, string rank, DateTimeOffset now) =>
        new() { CardId = cardId, Text = text.Trim(), Rank = rank, CreatedAt = now };

    public void Rewrite(string text) => Text = text.Trim();

    /// <returns>True when the state actually changed (so callers only log real changes).</returns>
    public bool SetDone(bool done)
    {
        if (IsDone == done)
        {
            return false;
        }

        IsDone = done;
        return true;
    }

    public void MoveTo(string rank) => Rank = rank;

    public void Rerank(string rank) => Rank = rank;
}
