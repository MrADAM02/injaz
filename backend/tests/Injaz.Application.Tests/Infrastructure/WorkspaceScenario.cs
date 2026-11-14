using Injaz.Application.Features.Auth;
using Injaz.Application.Features.Invitations;
using Injaz.Application.Features.Workspaces;
using Injaz.Domain.Workspaces;

namespace Injaz.Application.Tests.Infrastructure;

/// <summary>Builds workspaces with members through the real use cases (invite → register → accept).</summary>
public static class WorkspaceScenario
{
    public static async Task<(AuthSession Owner, WorkspaceDto Workspace)> CreateWorkspaceAsync(this TestApp app, string name = "Product")
    {
        ArgumentNullException.ThrowIfNull(app);
        var owner = await app.RegisterAsync();
        var workspace = await app.SendAsync(new CreateWorkspace(name));
        return (owner, workspace);
    }

    /// <summary>Adds a new user with <paramref name="role"/>; afterwards the new member is the current caller.</summary>
    public static async Task<AuthSession> AddMemberAsync(this TestApp app, AuthSession inviter, Guid workspaceId, WorkspaceRole role)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.ActAs(inviter);
        var email = TestData.UniqueEmail();
        var invitation = await app.SendAsync(new CreateInvitation(workspaceId, email, role));

        var member = await app.RegisterAsync(email: email);
        await app.SendAsync(new AcceptInvitation(invitation.Token));
        return member;
    }
}
