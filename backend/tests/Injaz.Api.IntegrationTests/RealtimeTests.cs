using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Injaz.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Injaz.Api.IntegrationTests;

/// <summary>End-to-end SignalR: a real client against the test server (long polling; TestServer has no WebSockets).</summary>
public sealed class RealtimeTests(ApiFactory factory)
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(1);

    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Changes_reach_other_members_live_but_not_the_connection_that_made_them()
    {
        var (owner, memberToken, workspaceId, boardId, listId) = await BoardWithMemberAsync();
        await using var ownerLive = await LiveClient.ConnectAsync(factory, owner);
        await using var memberLive = await LiveClient.ConnectAsync(factory, memberToken);
        await ownerLive.JoinAsync(boardId);
        await memberLive.JoinAsync(boardId);

        var created = await SendAsync(owner, HttpMethod.Post, $"/api/lists/{listId}/cards", new { title = "Live!" }, ownerLive.ConnectionId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var e = await memberLive.NextAsync();
        Assert.Equal("card.upserted", e.GetProperty("type").GetString());
        Assert.Equal(boardId, e.GetProperty("boardId").GetString());
        Assert.Equal("Live!", e.GetProperty("payload").GetProperty("title").GetString());
        Assert.Equal("none", e.GetProperty("payload").GetProperty("priority").GetString()); // same JSON conventions as REST

        await ownerLive.AssertQuietAsync(); // the originating connection already has it
        Assert.NotNull(workspaceId);
    }

    [Fact]
    public async Task Joining_a_board_requires_access_to_it()
    {
        var (_, _, _, boardId, _) = await BoardWithMemberAsync();
        var stranger = (await _client.RegisterAsync()).AccessToken;
        await using var live = await LiveClient.ConnectAsync(factory, stranger);

        var ex = await Assert.ThrowsAsync<HubException>(() => live.JoinAsync(boardId));

        Assert.Contains("board.notFound", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_connections_are_refused()
    {
        var connection = LiveClient.Build(factory, token: null);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => connection.StartAsync(Ct));
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task A_removed_member_is_told_and_stops_receiving_the_board()
    {
        var (owner, memberToken, workspaceId, boardId, listId) = await BoardWithMemberAsync();
        await using var memberLive = await LiveClient.ConnectAsync(factory, memberToken);
        await memberLive.JoinAsync(boardId);
        var memberId = (await (await SendAsync(memberToken, HttpMethod.Get, "/api/me")).JsonAsync()).GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(owner, HttpMethod.Delete, $"/api/workspaces/{workspaceId}/members/{memberId}")).StatusCode);

        var revoked = await memberLive.NextRevocationAsync();
        Assert.Equal(workspaceId, revoked.GetProperty("workspaceId").GetString());

        await SendAsync(owner, HttpMethod.Post, $"/api/lists/{listId}/cards", new { title = "secret" });
        await memberLive.AssertQuietAsync();
    }

    private async Task<(string Owner, string Member, string WorkspaceId, string BoardId, string ListId)> BoardWithMemberAsync()
    {
        var owner = (await _client.RegisterAsync()).AccessToken;
        var workspaceId = await CreateAsync(owner, "/api/workspaces", new { name = "Live" });
        var boardId = await CreateAsync(owner, $"/api/workspaces/{workspaceId}/boards", new { title = "Board" });
        var listId = await CreateAsync(owner, $"/api/boards/{boardId}/lists", new { title = "To do" });

        var email = HttpTestExtensions.UniqueEmail();
        var invite = await (await SendAsync(owner, HttpMethod.Post, $"/api/workspaces/{workspaceId}/invitations", new { email, role = "member" })).JsonAsync();
        var member = (await _client.RegisterAsync(email)).AccessToken;
        await SendAsync(member, HttpMethod.Post, $"/api/invitations/{invite.GetProperty("token").GetString()}/accept");

        return (owner, member, workspaceId, boardId, listId);
    }

    private async Task<string> CreateAsync(string token, string path, object body)
    {
        var response = await SendAsync(token, HttpMethod.Post, path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.JsonAsync()).GetProperty("id").GetString()!;
    }

    private async Task<HttpResponseMessage> SendAsync(string token, HttpMethod method, string path, object? body = null, string? connectionId = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (connectionId is not null)
        {
            request.Headers.Add("X-Connection-Id", connectionId);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, Ct);
    }

    private sealed class LiveClient : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly Channel<JsonElement> _events = Channel.CreateUnbounded<JsonElement>();
        private readonly Channel<JsonElement> _revocations = Channel.CreateUnbounded<JsonElement>();

        private LiveClient(HubConnection connection)
        {
            _connection = connection;
            _connection.On<JsonElement>("boardEvent", e => _events.Writer.TryWrite(e));
            _connection.On<JsonElement>("accessRevoked", e => _revocations.Writer.TryWrite(e));
        }

        public string ConnectionId => _connection.ConnectionId!;

        public static HubConnection Build(WebApplicationFactory<Program> factory, string? token) =>
            new HubConnectionBuilder()
                .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/boards"), options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    if (token is not null)
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    }
                })
                .Build();

        public static async Task<LiveClient> ConnectAsync(WebApplicationFactory<Program> factory, string token)
        {
            var client = new LiveClient(Build(factory, token));
            await client._connection.StartAsync(Ct);
            return client;
        }

        public Task JoinAsync(string boardId) => _connection.InvokeAsync("JoinBoard", Guid.Parse(boardId), Ct);

        public Task<JsonElement> NextAsync() => ReadAsync(_events);

        public Task<JsonElement> NextRevocationAsync() => ReadAsync(_revocations);

        public async Task AssertQuietAsync()
        {
            await Task.Delay(Quiet, Ct);
            Assert.False(_events.Reader.TryRead(out var unexpected), $"Unexpected event: {unexpected}");
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();

        private static async Task<JsonElement> ReadAsync(Channel<JsonElement> channel)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(Wait);
            return await channel.Reader.ReadAsync(timeout.Token);
        }
    }
}
