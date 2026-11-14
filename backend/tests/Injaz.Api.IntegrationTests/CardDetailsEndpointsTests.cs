using System.Net;
using System.Text.Json;
using Injaz.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Injaz.Api.IntegrationTests;

public sealed class CardDetailsEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Card_details_feeds_tasks_and_search_work_over_http()
    {
        var token = (await _client.RegisterAsync()).AccessToken;
        var me = (await (await Send(token, HttpMethod.Get, "/api/me")).JsonAsync()).GetProperty("id").GetString();
        var workspace = await Create(token, "/api/workspaces", new { name = "W" });
        var board = await Create(token, $"/api/workspaces/{workspace}/boards", new { title = "B" });
        var list = await Create(token, $"/api/boards/{board}/lists", new { title = "To do" });
        var card = await Create(token, $"/api/lists/{list}/cards", new { title = "Write spec" });
        var label = await Create(token, $"/api/boards/{board}/labels", new { name = "Bug", color = "red" });

        // Checklist + comments.
        var item = await Create(token, $"/api/cards/{card}/checklist", new { text = "Outline" });
        var done = await (await Send(token, HttpMethod.Patch, $"/api/checklist-items/{item}", new { isDone = true })).JsonAsync();
        Assert.True(done.GetProperty("isDone").GetBoolean());
        await Create(token, $"/api/cards/{card}/comments", new { body = "first" });
        await Create(token, $"/api/cards/{card}/comments", new { body = "second" });

        var firstPage = await (await Send(token, HttpMethod.Get, $"/api/cards/{card}/comments?limit=1")).JsonAsync();
        var cursor = firstPage.GetProperty("nextCursor").GetString();
        var secondPage = await (await Send(token, HttpMethod.Get, $"/api/cards/{card}/comments?limit=1&cursor={cursor}")).JsonAsync();
        Assert.Single(firstPage.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, secondPage.GetProperty("nextCursor").ValueKind);

        // Assign, label, due today in Riyadh.
        Assert.Equal(HttpStatusCode.NoContent, (await Send(token, HttpMethod.Put, $"/api/cards/{card}/assignees/{me}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(token, HttpMethod.Put, $"/api/cards/{card}/labels/{label}")).StatusCode);
        var riyadhNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh"));
        var endOfRiyadhDay = new DateTimeOffset(riyadhNow.Date.AddHours(23).AddMinutes(59), TimeSpan.FromHours(3));
        await Send(token, HttpMethod.Patch, $"/api/cards/{card}", new { dueAt = endOfRiyadhDay });

        // Board tile badges.
        var tile = (await (await Send(token, HttpMethod.Get, $"/api/boards/{board}")).JsonAsync()).GetProperty("lists")[0].GetProperty("cards")[0];
        Assert.Equal((1, 1, 2), (tile.GetProperty("checklistDone").GetInt32(), tile.GetProperty("checklistTotal").GetInt32(), tile.GetProperty("commentCount").GetInt32()));

        // My tasks (enum + time zone in the query string).
        var tasks = await (await Send(token, HttpMethod.Get, "/api/me/tasks?due=today&tz=Asia/Riyadh")).JsonAsync();
        var task = Assert.Single(tasks.GetProperty("items").EnumerateArray());
        Assert.Equal("B", task.GetProperty("boardTitle").GetString());

        // Search with repeated ids.
        var search = await (await Send(token, HttpMethod.Get, $"/api/boards/{board}/cards?q=SPEC&labelIds={label}&labelIds={Guid.NewGuid()}")).JsonAsync();
        Assert.Equal(card, Assert.Single(search.GetProperty("items").EnumerateArray()).GetProperty("card").GetProperty("id").GetString());

        // Activity: data is a JSON object, not a string.
        var feed = await (await Send(token, HttpMethod.Get, $"/api/cards/{card}/activity")).JsonAsync();
        var labelAdded = feed.GetProperty("items").EnumerateArray().Single(a => a.GetProperty("type").GetString() == "card.labelAdded");
        Assert.Equal("Bug", labelAdded.GetProperty("data").GetProperty("name").GetString());
        Assert.Equal(me, labelAdded.GetProperty("actor").GetProperty("userId").GetString());
    }

    [Fact]
    public async Task Bad_query_values_are_field_errors()
    {
        var token = (await _client.RegisterAsync()).AccessToken;

        var badZone = await Send(token, HttpMethod.Get, "/api/me/tasks?due=today&tz=Nowhere/City");
        var badBucket = await Send(token, HttpMethod.Get, "/api/me/tasks?due=someday");

        Assert.True((await badZone.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors").TryGetProperty("tz", out _));
        Assert.True((await badBucket.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors").TryGetProperty("due", out _));
    }

    private async Task<string> Create(string token, string path, object body)
    {
        var response = await Send(token, HttpMethod.Post, path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.JsonAsync()).GetProperty("id").GetString()!;
    }

    private Task<HttpResponseMessage> Send(string token, HttpMethod method, string path, object? body = null) =>
        _client.SendWithBearerAsync(method, path, token, body);
}
