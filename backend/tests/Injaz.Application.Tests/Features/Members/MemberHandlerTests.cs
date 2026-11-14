using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Members;
using Injaz.Application.Features.Workspaces;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Common;
using Injaz.Domain.Workspaces;

namespace Injaz.Application.Tests.Features.Members;

public sealed class MemberHandlerTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    [Fact]
    public async Task Members_are_listed_owner_first_then_by_role()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Viewer);
        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Admin);
        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Member);

        var members = await _app.QueryAsync(new ListMembers(workspace.Id)); // as the last-added member

        Assert.Equal(
            [WorkspaceRole.Owner, WorkspaceRole.Admin, WorkspaceRole.Member, WorkspaceRole.Viewer],
            members.Select(m => m.Role));
        Assert.Equal(owner.User.Email, members[0].Email);
    }

    [Fact]
    public async Task Admin_can_change_roles_of_non_owners()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        var viewer = await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Viewer);
        var admin = await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Admin);

        var promoted = await _app.SendAsync(new ChangeMemberRole(workspace.Id, viewer.User.Id, WorkspaceRole.Admin));

        Assert.Equal(WorkspaceRole.Admin, promoted.Role);
        Assert.Equal(viewer.User.DisplayName, promoted.DisplayName);

        var onOwner = await Assert.ThrowsAsync<DomainException>(() => _app.SendAsync(new ChangeMemberRole(workspace.Id, owner.User.Id, WorkspaceRole.Member)));
        Assert.Equal(WorkspaceErrors.CannotChangeOwnerRole, onOwner.Code);
        Assert.NotNull(admin);
    }

    [Fact]
    public async Task Role_change_validates_input_and_target()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        var member = await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Member);
        _app.ActAs(owner);

        var missing = await Assert.ThrowsAsync<ValidationException>(() => _app.SendAsync(new ChangeMemberRole(workspace.Id, member.User.Id, null)));
        var toOwner = await Assert.ThrowsAsync<DomainException>(() => _app.SendAsync(new ChangeMemberRole(workspace.Id, member.User.Id, WorkspaceRole.Owner)));
        var stranger = await Assert.ThrowsAsync<NotFoundException>(() => _app.SendAsync(new ChangeMemberRole(workspace.Id, Guid.CreateVersion7(), WorkspaceRole.Viewer)));

        Assert.Equal("required", Assert.Single(missing.Errors["role"]).Code);
        Assert.Equal(WorkspaceErrors.OwnerRoleNotAssignable, toOwner.Code);
        Assert.Equal("member.notFound", stranger.Code);
    }

    [Fact]
    public async Task Members_cannot_change_roles()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        var viewer = await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Viewer);
        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Member);

        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new ChangeMemberRole(workspace.Id, viewer.User.Id, WorkspaceRole.Admin)));
    }

    [Fact]
    public async Task Any_member_can_leave_but_the_owner_cannot()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        var viewer = await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Viewer);

        await _app.SendAsync(new RemoveMember(workspace.Id, viewer.User.Id));

        Assert.Empty(await _app.QueryAsync(new ListMyWorkspaces()));

        _app.ActAs(owner);
        var ownerLeaving = await Assert.ThrowsAsync<DomainException>(() => _app.SendAsync(new RemoveMember(workspace.Id, owner.User.Id)));
        Assert.Equal(WorkspaceErrors.CannotRemoveOwner, ownerLeaving.Code);
    }

    [Fact]
    public async Task Removing_someone_else_requires_admin()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        var viewer = await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Viewer);
        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Member);

        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new RemoveMember(workspace.Id, viewer.User.Id)));

        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Admin);
        await _app.SendAsync(new RemoveMember(workspace.Id, viewer.User.Id));

        _app.ActAs(viewer);
        await Assert.ThrowsAsync<NotFoundException>(() => _app.QueryAsync(new GetWorkspace(workspace.Id)));
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
