namespace Injaz.Domain.Boards;

/// <summary>
/// Named colors for boards and labels. Names (not hex values) are stored so the frontend can map each one to
/// light- and dark-theme shades and keep text contrast accessible.
/// </summary>
public static class Palette
{
    public const string DefaultBoardColor = "blue";

    public static IReadOnlyList<string> Colors { get; } =
        ["green", "yellow", "orange", "red", "purple", "blue", "sky", "lime", "pink", "gray"];

    public static bool IsValid(string? color) => color is not null && Colors.Contains(color);
}
