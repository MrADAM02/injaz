using System.Text.Json;
using System.Text.Json.Serialization;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Domain.Activity;

namespace Injaz.Application.Features.Activity;

/// <summary>
/// Records activity from inside command handlers. The entry is added to the same unit of work, so it is committed
/// atomically with the change it describes — never logged for a change that was rolled back, never missing for one
/// that wasn't. The actor and workspace come from the authorized request.
/// </summary>
public interface IActivityLog
{
    void Record(string type, Guid boardId, Guid? cardId = null, object? data = null);
}

/// <remarks>
/// One command can record several entries (e.g. renamed + completed). Each gets a strictly later timestamp than
/// the previous one in the same request, so the feed — ordered by time — always shows them in recorded order
/// rather than in the arbitrary order of their ids.
/// </remarks>
internal sealed class ActivityLog(IAppDbContext db, ICurrentAccess currentAccess, TimeProvider clock) : IActivityLog
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly TimeSpan Tick = TimeSpan.FromMicroseconds(1); // PostgreSQL timestamp precision

    private DateTimeOffset _last = DateTimeOffset.MinValue;

    public void Record(string type, Guid boardId, Guid? cardId = null, object? data = null)
    {
        var access = currentAccess.Access;
        var now = clock.GetUtcNow();
        _last = now > _last ? now : _last + Tick;
        db.Activity.Add(ActivityEntry.Create(
            access.WorkspaceId,
            boardId,
            cardId,
            access.UserId,
            type,
            JsonSerializer.Serialize(data ?? new { }, Json),
            _last));
    }
}
