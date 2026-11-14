using Injaz.Application.Validation;

namespace Injaz.Application.Common;

/// <summary>Due-date buckets used by My Tasks and board search.</summary>
public enum DueBucket
{
    /// <summary>Due before now and not completed.</summary>
    Overdue,

    /// <summary>Due during the caller's current calendar day.</summary>
    Today,

    /// <summary>Due from the start of today through the next 7 days.</summary>
    Week,

    /// <summary>No due date.</summary>
    None,
}

/// <summary>
/// Resolves a <see cref="DueBucket"/> to an absolute time window. "Today" depends on where the user is, so the
/// client sends its IANA time zone (e.g. <c>Asia/Riyadh</c>); the default is UTC.
/// </summary>
internal readonly record struct DueWindow(DateTimeOffset? From, DateTimeOffset? To, bool NoDueDate, bool ExcludeCompleted)
{
    public static DueWindow For(DueBucket bucket, DateTimeOffset now, string? timeZone)
    {
        var zone = ResolveZone(timeZone);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var startOfToday = new DateTimeOffset(localNow.Date, zone.GetUtcOffset(localNow.Date));

        return bucket switch
        {
            DueBucket.Overdue => new(null, now, false, ExcludeCompleted: true),
            DueBucket.Today => new(startOfToday.ToUniversalTime(), startOfToday.AddDays(1).ToUniversalTime(), false, false),
            DueBucket.Week => new(startOfToday.ToUniversalTime(), startOfToday.AddDays(8).ToUniversalTime(), false, false),
            DueBucket.None => new(null, null, NoDueDate: true, false),
            _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, null),
        };
    }

    public static bool IsValidZone(string? timeZone) =>
        timeZone is null || TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _);

    private static TimeZoneInfo ResolveZone(string? timeZone) =>
        timeZone is null ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZone);
}

internal static class DueRules
{
    public static void Validate(ValidationContext context, DueBucket? due, string? timeZone)
    {
        context.RuleFor("due", due).IsInEnum();
        context.RuleFor("tz", timeZone).MaxLength(64).Must(DueWindow.IsValidZone, ValidationCodes.InvalidValue);
    }
}
