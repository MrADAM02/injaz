using System.Globalization;
using System.Text;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Validation;

namespace Injaz.Application.Common;

/// <summary>A page of a list that can grow without bound. Pass <see cref="NextCursor"/> back to get the next page.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    /// <summary>Builds a page from <c>limit + 1</c> fetched rows: the extra row only signals that more exist.</summary>
    internal static PagedResult<T> From<TRow>(IReadOnlyList<TRow> rows, int limit, Func<TRow, T> map, Func<TRow, KeysetCursor> cursorOf)
    {
        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;
        return new PagedResult<T>([.. page.Select(map)], hasMore ? cursorOf(page[^1]).Encode() : null);
    }
}

/// <summary>
/// Opaque keyset position: the sort value (a timestamp, possibly null) plus the row id as a tie-breaker.
/// Keyset paging stays fast and stable while new rows are being added, unlike offsets.
/// </summary>
internal readonly record struct KeysetCursor(DateTimeOffset? At, Guid Id)
{
    public string Encode()
    {
        var raw = $"{(At is { } at ? at.UtcTicks.ToString(CultureInfo.InvariantCulture) : "-")}|{Id:N}";
        return Convert.ToBase64String(Encoding.ASCII.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <exception cref="ValidationException">The cursor wasn't produced by <see cref="Encode"/>.</exception>
    public static KeysetCursor Decode(string cursor)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            var parts = Encoding.ASCII.GetString(Convert.FromBase64String(base64)).Split('|');

            DateTimeOffset? at = parts[0] == "-"
                ? null
                : new DateTimeOffset(long.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture), TimeSpan.Zero);
            return new KeysetCursor(at, Guid.ParseExact(parts[1], "N"));
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException or ArgumentOutOfRangeException)
        {
            throw ValidationException.ForField("cursor", ValidationCodes.InvalidValue);
        }
    }
}

internal static class PageRules
{
    public const int DefaultLimit = 30;
    public const int MaxLimit = 100;

    public static void Limit(ValidationContext context, int? limit) =>
        context.RuleFor("limit", limit ?? DefaultLimit).InclusiveBetween(1, MaxLimit);
}
