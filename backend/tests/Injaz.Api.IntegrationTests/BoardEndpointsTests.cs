using System.Net;
using System.Text.Json;
using Injaz.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Injaz.Api.IntegrationTests;

public sealed class BoardEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Board_columns_and_cards_round_trip_with_drag_and_drop()
    {
        var token = await SignUpAsync();
        var workspaceId = await CreateAsync(token, "/api/workspaces", new { name = "W" });
        var boardId = await CreateAsync(token, $"/api/workspaces/{workspaceId}/boards", new { title = "Roadmap", color = "purple" });
        var todo = await CreateAsync(token, $"/api/boards/{boardId}/lists", new { title = "To do" });
        var done = await CreateAsync(token, $"/api/boards/{boardId}/lists", new { title = "Done" });
        var a = await CreateAsync(token, $"/api/lists/{todo}/cards", new { title = "A" });
        var b = await CreateAsync(token, $"/api/lists/{todo}/cards", new { title = "B" });
        var top = await CreateAsync(token, $"/api/lists/{todo}/cards", new { title = "Top", position = "top" });

        // Drag "A" into "Done", then drag "Done" before "To do".
        var moved = await Send(token, HttpMethod.Post, $"/api/cards/{a}/move", new { listId = done });
        Assert.Equal(done, (await moved.JsonAsync()).GetProperty("listId").GetString());
        await Send(token, HttpMethod.Post, $"/api/lists/{done}/move", new { beforeId = todo });

        var board = await (await Send(token, HttpMethod.Get, $"/api/boards/{boardId}")).JsonAsync();
        Assert.Equal("purple", board.GetProperty("board").GetProperty("color").GetString());
        Assert.Equal("owner", board.GetProperty("myRole").GetString());
        var lists = board.GetProperty("lists").EnumerateArray().ToList();
        Assert.Equal(["Done", "To do"], lists.Select(l => l.GetProperty("title").GetString()));
        Assert.Equal(["A"], Titles(lists[0]));
        Assert.Equal(["Top", "B"], Titles(lists[1]));
        Assert.Equal(b, lists[1].GetProperty("cards")[1].GetProperty("id").GetString());
        Assert.NotNull(top);
    }

    [Fact]
    public async Task Patch_distinguishes_omitted_fields_from_explicit_nulls()
    {
        var (token, card) = await CardAsync();

        var set = await (await Send(token, HttpMethod.Patch, $"/api/cards/{card}", new { dueAt = "2026-12-01T17:00:00+03:00", priority = "urgent" })).JsonAsync();
        Assert.Equal("2026-12-01T14:00:00+00:00", set.GetProperty("dueAt").GetString());
        Assert.Equal("urgent", set.GetProperty("priority").GetString());

        // Omitting dueAt keeps it…
        var renamed = await (await Send(token, HttpMethod.Patch, $"/api/cards/{card}", new { title = "Renamed" })).JsonAsync();
        Assert.Equal("2026-12-01T14:00:00+00:00", renamed.GetProperty("dueAt").GetString());

        // …sending null clears it.
        var cleared = await (await Send(token, HttpMethod.Patch, $"/api/cards/{card}", JsonDocument.Parse("""{ "dueAt": null }""").RootElement)).JsonAsync();
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("dueAt").ValueKind);
        Assert.Equal("Renamed", cleared.GetProperty("title").GetString());
        Assert.Equal("urgent", cleared.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task Invalid_patch_values_are_field_errors()
    {
        var (token, card) = await CardAsync();

        var badEnum = await Send(token, HttpMethod.Patch, $"/api/cards/{card}", new { priority = "asap" });
        var nullTitle = await Send(token, HttpMethod.Patch, $"/api/cards/{card}", JsonDocument.Parse("""{ "title": null }""").RootElement);

        Assert.True((await badEnum.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors").TryGetProperty("priority", out _));
        var errors = (await nullTitle.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal("required", errors.GetProperty("title")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Dropping_next_to_a_card_that_is_gone_is_a_409()
    {
        var (token, card) = await CardAsync();

        var response = await Send(token, HttpMethod.Post, $"/api/cards/{card}/move", new { afterId = Guid.NewGuid() });

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "ordering.staleNeighbor");
    }

    [Fact]
    public async Task Cards_of_other_workspaces_are_invisible()
    {
        var (_, card) = await CardAsync();
        var stranger = await SignUpAsync();

        var response = await Send(stranger, HttpMethod.Get, $"/api/cards/{card}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "card.notFound");
    }

    private async Task<string> SignUpAsync() => (await _client.RegisterAsync()).AccessToken;

    private async Task<(string Token, string CardId)> CardAsync()
    {
        var token = await SignUpAsync();
        var workspaceId = await CreateAsync(token, "/api/workspaces", new { name = "W" });
        var boardId = await CreateAsync(token, $"/api/workspaces/{workspaceId}/boards", new { title = "B" });
        var listId = await CreateAsync(token, $"/api/boards/{boardId}/lists", new { title = "L" });
        return (token, await CreateAsync(token, $"/api/lists/{listId}/cards", new { title = "Card" }));
    }

    private async Task<string> CreateAsync(string token, string path, object body)
    {
        var response = await Send(token, HttpMethod.Post, path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.JsonAsync()).GetProperty("id").GetString()!;
    }

    private Task<HttpResponseMessage> Send(string token, HttpMethod method, string path, object? body = null) =>
        _client.SendWithBearerAsync(method, path, token, body);

    private static IEnumerable<string?> Titles(JsonElement list) =>
        list.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("title").GetString());
}
