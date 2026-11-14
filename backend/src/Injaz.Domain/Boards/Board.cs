using Injaz.Domain.Common;

namespace Injaz.Domain.Boards;

/// <summary>
/// A board in a workspace. Lists and cards are separate aggregates (a board can hold thousands of cards, and
/// moving one card must not lock or reload the whole board); they reference the board by id.
/// </summary>
public sealed class Board : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2_000;

    private Board()
    {
        // EF Core
    }

    public Guid WorkspaceId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public string Color { get; private set; } = Palette.DefaultBoardColor;

    public bool IsArchived { get; private set; }

    public Guid CreatedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Board Create(Guid workspaceId, string title, string? description, string? color, Guid createdById, DateTimeOffset now) =>
        new()
        {
            WorkspaceId = workspaceId,
            Title = title.Trim(),
            Description = Normalize(description),
            Color = color ?? Palette.DefaultBoardColor,
            CreatedById = createdById,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void Rename(string title, DateTimeOffset now) => Touch(() => Title = title.Trim(), now);

    public void Describe(string? description, DateTimeOffset now) => Touch(() => Description = Normalize(description), now);

    public void Recolor(string color, DateTimeOffset now) => Touch(() => Color = color, now);

    public void SetArchived(bool archived, DateTimeOffset now) => Touch(() => IsArchived = archived, now);

    private void Touch(Action change, DateTimeOffset now)
    {
        change();
        UpdatedAt = now;
    }

    private static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
