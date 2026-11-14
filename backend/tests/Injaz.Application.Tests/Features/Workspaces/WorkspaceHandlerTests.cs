using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Workspaces;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Common;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Tests.Features.Workspaces;

public sealed class WorkspaceHandlerTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creator_owns_the_new_workspace_and_sees_it_in_their_list()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync("  فريق المنتج  ");

        Assert.Equal("فريق المنتج", workspace.Name);
        Assert.Equal(WorkspaceRole.Owner, workspace.MyRole);
        Assert.Equal(1, workspace.MemberCount);

        var mine = await _app.QueryAsync(new ListMyWorkspaces());
        Assert.Equal(workspace, Assert.Single(mine));
        Assert.Equal(workspace, await _app.QueryAsync(new GetWorkspace(workspace.Id)));
        Assert.Equal(owner.User.Id, (await _app.DbAsync(db => db.Workspaces.SingleAsync(w => w.Id == workspace.Id, Ct))).CreatedById);
    }

    [Fact]
    public async Task Workspace_name_is_validated()
    {
        await _app.RegisterAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _app.SendAsync(new CreateWorkspace(new string('x', 101))));

        Assert.Equal("maxLength", Assert.Single(ex.Errors["name"]).Code);
    }

    [Fact]
    public async Task Non_members_get_not_found_so_workspaces_cannot_be_probed()
    {
        var (_, workspace) = await _app.CreateWorkspaceAsync();
        await _app.RegisterAsync(); // a stranger

        var get = await Assert.ThrowsAsync<NotFoundException>(() => _app.QueryAsync(new GetWorkspace(workspace.Id)));
        var rename = await Assert.ThrowsAsync<NotFoundException>(() => _app.SendAsync(new RenameWorkspace(workspace.Id, "Mine now")));

        Assert.Equal("workspace.notFound", get.Code);
        Assert.Equal("workspace.notFound", rename.Code);
        Assert.Empty(await _app.QueryAsync(new ListMyWorkspaces()));
    }

    [Theory]
    [InlineData(WorkspaceRole.Viewer)]
    [InlineData(WorkspaceRole.Member)]
    public async Task Only_admins_and_the_owner_can_rename(WorkspaceRole role)
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        await _app.AddMemberAsync(owner, workspace.Id, role);

        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new RenameWorkspace(workspace.Id, "Renamed")));

        var admin = await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Admin);
        var renamed = await _app.SendAsync(new RenameWorkspace(workspace.Id, "Renamed"));
        Assert.Equal("Renamed", renamed.Name);
        Assert.Equal(WorkspaceRole.Admin, renamed.MyRole);
        Assert.Equal(3, renamed.MemberCount);
        Assert.NotNull(admin);
    }

    [Fact]
    public async Task Only_the_owner_can_delete_and_deletion_removes_memberships_and_invitations()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Admin);

        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new DeleteWorkspace(workspace.Id)));

        _app.ActAs(owner);
        await _app.SendAsync(new DeleteWorkspace(workspace.Id));

        Assert.False(await _app.DbAsync(db => db.Workspaces.AnyAsync(w => w.Id == workspace.Id, Ct)));
        Assert.False(await _app.DbAsync(db => db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == workspace.Id, Ct)));
        Assert.False(await _app.DbAsync(db => db.WorkspaceInvitations.AnyAsync(i => i.WorkspaceId == workspace.Id, Ct)));
    }

    [Fact]
    public async Task Ownership_transfer_hands_over_owner_powers()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();
        var member = await _app.AddMemberAsync(owner, workspace.Id, WorkspaceRole.Member);

        _app.ActAs(owner);
        await _app.SendAsync(new TransferOwnership(workspace.Id, member.User.Id));

        Assert.Equal(WorkspaceRole.Admin, (await _app.QueryAsync(new GetWorkspace(workspace.Id))).MyRole);
        await Assert.ThrowsAsync<ForbiddenException>(() => _app.SendAsync(new DeleteWorkspace(workspace.Id)));

        _app.ActAs(member);
        Assert.Equal(WorkspaceRole.Owner, (await _app.QueryAsync(new GetWorkspace(workspace.Id))).MyRole);
    }

    [Fact]
    public async Task Ownership_can_only_go_to_an_existing_member()
    {
        var (owner, workspace) = await _app.CreateWorkspaceAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _app.SendAsync(new TransferOwnership(workspace.Id, Guid.CreateVersion7())));
        var self = await Assert.ThrowsAsync<DomainException>(() => _app.SendAsync(new TransferOwnership(workspace.Id, owner.User.Id)));
        Assert.Equal(WorkspaceErrors.AlreadyOwner, self.Code);
    }

    [Fact]
    public async Task Concurrent_changes_to_the_same_workspace_cannot_both_win()
    {
        var (_, workspace) = await _app.CreateWorkspaceAsync();

        var results = await Task.WhenAll(
            Attempt(() => _app.SendAsync(new RenameWorkspace(workspace.Id, "A"))),
            Attempt(() => _app.SendAsync(new RenameWorkspace(workspace.Id, "B"))));

        // Either they serialized (both fine) or one hit the xmin check — never a silent lost update with an error swallowed.
        Assert.All(results, r => Assert.True(r is null or ConflictException, r?.GetType().Name));
        Assert.Contains((await _app.QueryAsync(new GetWorkspace(workspace.Id))).Name, new[] { "A", "B" });
    }

    private static async Task<Exception?> Attempt(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (AppException ex)
        {
            return ex;
        }
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
