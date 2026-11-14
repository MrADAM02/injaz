namespace Injaz.Infrastructure.Time;

/// <summary>
/// PostgreSQL <c>timestamptz</c> stores microseconds; .NET ticks are 100 ns. Truncating "now" at the source means a
/// value held in memory (e.g. a DTO returned right after creation) is identical to what is later read back.
/// Registered as the application's <see cref="TimeProvider"/>, so handlers need no special care.
/// </summary>
public sealed class DatabasePrecisionTimeProvider(TimeProvider inner) : TimeProvider
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    public override DateTimeOffset GetUtcNow()
    {
        var now = inner.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TicksPerMicrosecond));
    }

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override long TimestampFrequency => inner.TimestampFrequency;

    public override long GetTimestamp() => inner.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        inner.CreateTimer(callback, state, dueTime, period);
}
