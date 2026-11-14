using System.Collections.Concurrent;
using System.Security.Claims;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Injaz.Api.Realtime;

/// <summary>
/// Live board updates. Clients call <see cref="JoinBoard"/> for the board on screen and listen for
/// <c>boardEvent</c> (a <see cref="BoardEvent"/>) and <c>accessRevoked</c>. Joining runs the same authorization as
/// reading the board. Connections close when their access token expires; clients reconnect with a fresh one.
/// </summary>
[Authorize]
public sealed class BoardHub(IDispatcher dispatcher, CallerPrincipal caller, RealtimeConnections connections) : Hub
{
    public const string Path = "/hubs/boards";
    public const string BoardEventMethod = "boardEvent";
    public const string AccessRevokedMethod = "accessRevoked";

    public static string Group(Guid boardId) => $"board:{boardId:N}";

    public override async Task OnConnectedAsync()
    {
        connections.Add(UserId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(UserId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <exception cref="HubException">With the error code, e.g. <c>board.notFound</c> for boards the caller can't see.</exception>
    public async Task JoinBoard(Guid boardId)
    {
        caller.Principal = Context.User;
        try
        {
            await dispatcher.QueryAsync(new SubscribeToBoard(boardId), Context.ConnectionAborted);
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Code); // only HubException messages reach the client
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(boardId), Context.ConnectionAborted);
    }

    public Task LeaveBoard(Guid boardId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(boardId), Context.ConnectionAborted);

    private Guid UserId => Guid.Parse(Context.UserIdentifier!);
}

/// <summary>
/// Hub invocations don't run inside an HTTP request, so <c>ICurrentUser</c> can't rely on the HTTP context there;
/// the hub stores the caller here first (scoped per invocation).
/// </summary>
public sealed class CallerPrincipal
{
    public ClaimsPrincipal? Principal { get; set; }
}

/// <summary>SignalR's user id is the JWT <c>sub</c> (inbound claim mapping is off).</summary>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirstValue("sub");
}

/// <summary>
/// Which live connections each user has — needed to pull a removed member's connections out of board groups.
/// In-memory, so it matches the single-instance deployment; scaling out needs a backplane (e.g. Redis) and a
/// shared registry.
/// </summary>
public sealed class RealtimeConnections
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _byUser = new();

    public void Add(Guid userId, string connectionId) =>
        _byUser.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>())[connectionId] = 0;

    public void Remove(Guid userId, string connectionId)
    {
        if (_byUser.TryGetValue(userId, out var set))
        {
            set.TryRemove(connectionId, out _);
        }
    }

    public IReadOnlyCollection<string> Of(Guid userId) =>
        _byUser.TryGetValue(userId, out var set) ? [.. set.Keys] : [];
}
