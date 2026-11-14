using Injaz.Domain.Common;

namespace Injaz.Domain.Boards;

/// <summary>A board-scoped label. The name is optional (a color alone is a valid label, as in Trello).</summary>
public sealed class Label : Entity
{
    public const int NameMaxLength = 50;

    private Label()
    {
        // EF Core
    }

    public Guid BoardId { get; private set; }

    public string? Name { get; private set; }

    public string Color { get; private set; } = null!;

    public static Label Create(Guid boardId, string? name, string color) =>
        new() { BoardId = boardId, Name = Normalize(name), Color = color };

    public void Rename(string? name) => Name = Normalize(name);

    public void Recolor(string color) => Color = color;

    private static string? Normalize(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();
}
