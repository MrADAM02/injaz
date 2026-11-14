using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Invitations;
using Injaz.Application.Features.Members;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Common;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Tests.Features.Invitations;

public sealed class InvitationHandlerTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Invitation_link_is_returned_once_stored_hashed_and_emailed_after_commit()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync("Design");
        var email = TestData.UniqueEmail();

        var created = await _app.SendAsync(new CreateInvitation(workspace.Id, email.ToUpperInvariant(), WorkspaceRole.Member));

        Assert.Equal(email, created.Invitation.Email);
        Assert.Equal(owner.User.DisplayName, created.Invitation.InvitedByName);
        Assert.EndsWith($"/invite/{created.Token}", created.AcceptUrl, StringComparison.Ordinal);

        var stored = await _app.DbAsync(db => db.WorkspaceInvitations.SingleAsync(i => i.Id == created.Invitation.Id, Ct));
        Assert.NotEqual(created.Token, stored.TokenHash);

        var mail = Assert.Single(_app.Emails.Sent, m => m.To == email);
        Assert.Contains("Design", mail.Subject, StringComparison.Ordinal);
        Assert.Contains(created.AcceptUrl, mail.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invitee_can_preview_then_accept_and_joins_with_the_invited_role()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync("Design");
        var email = TestData.UniqueEmail();
        var created = await _app.SendAsync(new CreateInvitation(workspace.Id, email, WorkspaceRole.Viewer));

        _app.CurrentUser.Id = null; // preview works before signing in
        var preview = await _app.QueryAsync(new GetInvitation(created.Token));
        Assert.Equal(("Design", owner.User.DisplayName, InvitationStatus.Pending), (preview.WorkspaceName, preview.InvitedByName, preview.Status));

        await _app.RegisterAsync(email: email);
        var joined = await _app.SendAsync(new AcceptInvitation(created.Token));

        Assert.Equal(WorkspaceRole.Viewer, joined.MyRole);
        Assert.Equal(2, joined.MemberCount);
        Assert.Equal(InvitationStatus.Accepted, (await _app.QueryAsync(new GetInvitation(created.Token))).Status);

        var again = await Assert.ThrowsAsync<DomainException>(() => _app.SendAsync(new AcceptInvitation(created.Token)));
        Assert.Equal("invitation.accepted", again.Code);
    }

    [Fact]
    public async Task Invitation_can_only_be_accepted_by_the_invited_email()
    {
        var (_, workspace) = await _app.CreateWorkspaceAsync();
        var created = await _app.SendAsync(new CreateInvitation(workspace.Id, TestData.UniqueEmail(), WorkspaceRole.Member));

        await _app.RegisterAsync(); // someone the link was forwarded to

        var ex = await Assert.ThrowsAsync<DomainException>(() => _app.SendAsync(new AcceptInvitation(created.Token)));
        Assert.Equal(InvitationErrors.EmailMismatch, ex.Code);
    }

    [Fact]
    public async Task Expired_invitations_cannot_be_accepted()
    {
        var (_, workspace) = await _app.CreateWorkspaceAsync();
        var email = TestData.UniqueEmail();
        var created = await _app.SendAsync(new CreateInvitation(workspace.Id, email, WorkspaceRole.Member));
        await _app.RegisterAsync(email: email);

        _app.Clock.Advance(TimeSpan.FromDays(8));

        var ex = await Assert.ThrowsAsync<DomainException>(() => _app.SendAsync(new AcceptInvitation(created.Token)));
        Assert.Equal("invitation.expired", ex.Code);
    }

    [Fact]
    public async Task Re_inviting_revokes_the_previous_link()
    {
        var (_, workspace) = await _app.CreateWorkspaceAsync();
        var email = TestData.UniqueEmail();
        var first = await _app.SendAsync(new CreateInvitation(workspace.Id, email, WorkspaceRole.Viewer));
        var second = await _app.SendAsync(new CreateInvitation(workspace.Id, email, WorkspaceRole.Admin));

        var pending = await _app.QueryAsync(new ListInvitations(workspace.Id));
        Assert.Equal(second.Invitation.Id, Assert.Single(pending).Id);

        await _app.RegisterAsync(email: email);
        var stale = await Assert.ThrowsAsync<DomainException>(() => _app.SendAsync(new AcceptInvitation(first.Token)));
        Assert.Equal("invitation.revoked", stale.Code);
        Assert.Equal(WorkspaceRole.Admin, (await _app.SendAsync(new AcceptInvitation(second.Token))).MyRole);
    }

    [Fact]
    public async Task Inviting_an_existing_member_is_a_conflict()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => _app.SendAsync(new CreateInvitation(workspace.Id, owner.User.Email.ToUpperInvariant(), WorkspaceRole.Member)));

        Assert.Equal(WorkspaceErrors.AlreadyMember, ex.Code);
    }

    [Fact]
    public async Task Invitation_input_is_validated_and_owner_role_is_rejected()
    {
        var (_, workspace) = await _app.CreateWorkspaceAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _app.SendAsync(new CreateInvitation(workspace.Id, "nope", WorkspaceRole.Owner)));

        Assert.Equal("email", Assert.Single(ex.Errors["email"]).Code);
        Assert.Equal("invalidValue", Assert.Single(ex.Errors["role"]).Code);
    }

    [Fact]
    public async Task Members_below_admin_cannot_see_or_send_invitations()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Member);

        await Assert.ThrowsAsync<ForbiddenException>(() => _app.QueryAsync(new ListInvitations(workspace.Id)));
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new CreateInvitation(workspace.Id, TestData.UniqueEmail(), WorkspaceRole.Viewer)));
    }

    [Fact]
    public async Task Revoked_invitation_disappears_and_cannot_be_revoked_from_another_workspace()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        var created = await _app.SendAsync(new CreateInvitation(workspace.Id, TestData.UniqueEmail(), WorkspaceRole.Member));

        var (_, otherWorkspace) = await _app.CreateWorkspaceAsync(); // a different owner, now the caller
        await Assert.ThrowsAsync<NotFoundException>(() => _app.SendAsync(new RevokeInvitation(otherWorkspace.Id, created.Invitation.Id)));

        _app.ActAs(owner);
        await _app.SendAsync(new RevokeInvitation(workspace.Id, created.Invitation.Id));
        Assert.Empty(await _app.QueryAsync(new ListInvitations(workspace.Id)));
        Assert.Equal(InvitationStatus.Revoked, (await _app.QueryAsync(new GetInvitation(created.Token))).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token")]
    public async Task Unknown_tokens_are_not_found(string? token)
    {
        await _app.RegisterAsync();

        Assert.Equal("invitation.notFound", (await Assert.ThrowsAsync<NotFoundException>(() => _app.QueryAsync(new GetInvitation(token)))).Code);
        await Assert.ThrowsAsync<NotFoundException>(() => _app.SendAsync(new AcceptInvitation(token)));
    }

    [Fact]
    public async Task Accepted_member_appears_in_the_member_list()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        var member = await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Member);

        var members = await _app.QueryAsync(new ListMembers(workspace.Id));

        Assert.Contains(members, m => m.UserId == member.User.Id && m.Role == WorkspaceRole.Member);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
