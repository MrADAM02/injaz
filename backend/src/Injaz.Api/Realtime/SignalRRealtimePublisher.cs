using Injaz.Api.Common;
using Injaz.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Injaz.Api.Realtime;

/// <summary>
/// Delivers board events to the board's SignalR group. The client sends its connection id in
/// <c>X-Connection-Id</c> on REST calls; that connection is skipped because it already applied the change
/// optimistically.
/// </summary>
internal sealed class SignalRRealtimePublisher(
    IHubContext<BoardHub> hub,
    IHttpContextAccessor httpContextAccessor,
    RealtimeConnections connections) : IRealtimePublisher
{
    public Task PublishAsync(BoardEvent boardEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(boardEvent);

        var group = BoardHub.Group(boardEvent.BoardId);
        var origin = httpContextAccessor.HttpContext?.Request.Headers[ApiServiceCollectionExtensions.ConnectionIdHeader].ToString();
        var clients = string.IsNullOrWhiteSpace(origin) ? hub.Clients.Group(group) : hub.Clients.GroupExcept(group, origin);

        return clients.SendAsync(BoardHub.BoardEventMethod, boardEvent, cancellationToken);
    }

    public async Task RevokeAsync(Guid userId, Guid workspaceId, IReadOnlyCollection<Guid> boardIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(boardIds);

        foreach (var connectionId in connections.Of(userId))
        {
            foreach (var boardId in boardIds)
            {
                await hub.Groups.RemoveFromGroupAsync(connectionId, BoardHub.Group(boardId), cancellationToken);
            }
        }

        await hub.Clients.User(userId.ToString()).SendAsync(BoardHub.AccessRevokedMethod, new { workspaceId }, cancellationToken);
    }
}
