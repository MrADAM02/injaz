using System.Net;
using System.Net.Http.Json;
using Injaz.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Injaz.Api.IntegrationTests;

public sealed class WorkspaceEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Full_workspace_lifecycle_over_http()
    {
        // Owner creates a workspace.
        var (owner, _, _) = await _client.RegisterAsync();
        var create = await _client.SendWithBearerAsync(HttpMethod.Post, "/api/workspaces", owner, new { name = "فريق المنتج" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var workspace = await create.JsonAsync();
        var id = workspace.GetProperty("id").GetString();
        Assert.Equal($"/api/workspaces/{id}", create.Headers.Location?.OriginalString);
        Assert.Equal("owner", workspace.GetProperty("myRole").GetString());

        var list = await (await _client.SendWithBearerAsync(HttpMethod.Get, "/api/workspaces", owner)).JsonAsync();
        Assert.Equal(id, Assert.Single(list.EnumerateArray()).GetProperty("id").GetString());

        // Owner invites a member by email; the link token comes back once.
        var inviteeEmail = HttpTestExtensions.UniqueEmail();
        var invite = await _client.SendWithBearerAsync(HttpMethod.Post, $"/api/workspaces/{id}/invitations", owner, new { email = inviteeEmail, role = "member" });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var token = (await invite.JsonAsync()).GetProperty("token").GetString();

        // The invite page can preview it anonymously.
        var preview = await (await _client.GetAsync($"/api/invitations/{token}", Ct)).JsonAsync();
        Assert.Equal("pending", preview.GetProperty("status").GetString());
        Assert.Equal("فريق المنتج", preview.GetProperty("workspaceName").GetString());

        // Invitee signs up with the invited email and accepts.
        var (invitee, _, _) = await _client.RegisterAsync(inviteeEmail);
        var accept = await _client.SendWithBearerAsync(HttpMethod.Post, $"/api/invitations/{token}/accept", invitee);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal("member", (await accept.JsonAsync()).GetProperty("myRole").GetString());

        // A member can't rename; a stranger can't even see it.
        var forbidden = await _client.SendWithBearerAsync(HttpMethod.Patch, $"/api/workspaces/{id}", invitee, new { name = "Hijacked" });
        await forbidden.AssertProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");

        var (stranger, _, _) = await _client.RegisterAsync();
        var hidden = await _client.SendWithBearerAsync(HttpMethod.Get, $"/api/workspaces/{id}", stranger);
        await hidden.AssertProblemAsync(HttpStatusCode.NotFound, "workspace.notFound");

        // Members are listed owner first; the owner promotes the invitee.
        var members = await (await _client.SendWithBearerAsync(HttpMethod.Get, $"/api/workspaces/{id}/members", invitee)).JsonAsync();
        Assert.Equal(["owner", "member"], members.EnumerateArray().Select(m => m.GetProperty("role").GetString()));

        var inviteeId = members[1].GetProperty("userId").GetString();
        var promote = await _client.SendWithBearerAsync(HttpMethod.Patch, $"/api/workspaces/{id}/members/{inviteeId}", owner, new { role = "admin" });
        Assert.Equal("admin", (await promote.JsonAsync()).GetProperty("role").GetString());

        // Owner deletes the workspace; it's gone for everyone.
        var delete = await _client.SendWithBearerAsync(HttpMethod.Delete, $"/api/workspaces/{id}", owner);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var gone = await _client.SendWithBearerAsync(HttpMethod.Get, $"/api/workspaces/{id}", invitee);
        await gone.AssertProblemAsync(HttpStatusCode.NotFound, "workspace.notFound");
    }

    [Fact]
    public async Task Unknown_enum_values_are_rejected_as_validation_errors()
    {
        var (owner, _, _) = await _client.RegisterAsync();
        var created = await (await _client.SendWithBearerAsync(HttpMethod.Post, "/api/workspaces", owner, new { name = "W" })).JsonAsync();
        var id = created.GetProperty("id").GetString();

        var response = await _client.SendWithBearerAsync(
            HttpMethod.Post, $"/api/workspaces/{id}/invitations", owner, new { email = HttpTestExtensions.UniqueEmail(), role = "superuser" });

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("role", out _));
    }

    [Fact]
    public async Task Business_rule_violations_return_422_with_a_code()
    {
        var (owner, _, _) = await _client.RegisterAsync();
        var me = await (await _client.SendWithBearerAsync(HttpMethod.Get, "/api/me", owner)).JsonAsync();
        var created = await (await _client.SendWithBearerAsync(HttpMethod.Post, "/api/workspaces", owner, new { name = "W" })).JsonAsync();
        var id = created.GetProperty("id").GetString();

        var leave = await _client.SendWithBearerAsync(HttpMethod.Delete, $"/api/workspaces/{id}/members/{me.GetProperty("id").GetString()}", owner);

        await leave.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "workspace.cannotRemoveOwner");
    }

    [Fact]
    public async Task Invitation_preview_for_an_unknown_token_is_404() =>
        await (await _client.GetAsync("/api/invitations/nope", Ct)).AssertProblemAsync(HttpStatusCode.NotFound, "invitation.notFound");

    [Fact]
    public async Task Invitation_accept_requires_sign_in()
    {
        var response = await _client.PostAsJsonAsync("/api/invitations/whatever/accept", new { }, Ct);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }
}
