using Injaz.Domain.Common;
using Injaz.Domain.Workspaces;

namespace Injaz.Domain.Tests.Workspaces;

public sealed class WorkspaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _owner = Guid.CreateVersion7();
    private readonly Guid _alice = Guid.CreateVersion7();

    private Workspace NewWorkspace() => Workspace.Create("  Product  ", _owner, Now);

    [Fact]
    public void Creator_becomes_the_single_owner()
    {
        var workspace = NewWorkspace();

        Assert.Equal("Product", workspace.Name);
        var member = Assert.Single(workspace.Members);
        Assert.Equal(_owner, member.UserId);
        Assert.Equal(WorkspaceRole.Owner, member.Role);
    }

    [Fact]
    public void Adding_the_same_user_twice_is_rejected()
    {
        var workspace = NewWorkspace();
        workspace.AddMember(_alice, WorkspaceRole.Member, Now);

        AssertRule(WorkspaceErrors.AlreadyMember, () => workspace.AddMember(_alice, WorkspaceRole.Viewer, Now));
    }

    [Fact]
    public void Owner_role_can_never_be_assigned_directly()
    {
        var workspace = NewWorkspace();
        workspace.AddMember(_alice, WorkspaceRole.Member, Now);

        AssertRule(WorkspaceErrors.OwnerRoleNotAssignable, () => workspace.AddMember(Guid.CreateVersion7(), WorkspaceRole.Owner, Now));
        AssertRule(WorkspaceErrors.OwnerRoleNotAssignable, () => workspace.ChangeRole(_alice, WorkspaceRole.Owner, Now));
    }

    [Fact]
    public void Owner_cannot_be_demoted_or_removed()
    {
        var workspace = NewWorkspace();

        AssertRule(WorkspaceErrors.CannotChangeOwnerRole, () => workspace.ChangeRole(_owner, WorkspaceRole.Admin, Now));
        AssertRule(WorkspaceErrors.CannotRemoveOwner, () => workspace.RemoveMember(_owner, Now));
        Assert.Equal(_owner, workspace.Owner.UserId);
    }

    [Fact]
    public void Transfer_swaps_owner_and_keeps_exactly_one()
    {
        var workspace = NewWorkspace();
        workspace.AddMember(_alice, WorkspaceRole.Member, Now);

        workspace.TransferOwnership(_alice, Now.AddMinutes(1));

        Assert.Equal(_alice, workspace.Owner.UserId);
        Assert.Equal(WorkspaceRole.Admin, workspace.FindMember(_owner)!.Role);
        Assert.Single(workspace.Members, m => m.Role == WorkspaceRole.Owner);
        Assert.Equal(Now.AddMinutes(1), workspace.UpdatedAt);
    }

    [Fact]
    public void Transfer_requires_an_existing_other_member()
    {
        var workspace = NewWorkspace();

        AssertRule(WorkspaceErrors.MemberNotFound, () => workspace.TransferOwnership(_alice, Now));
        AssertRule(WorkspaceErrors.AlreadyOwner, () => workspace.TransferOwnership(_owner, Now));
    }

    [Fact]
    public void Every_membership_change_touches_the_aggregate()
    {
        var workspace = NewWorkspace();

        workspace.AddMember(_alice, WorkspaceRole.Viewer, Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(1), workspace.UpdatedAt);

        workspace.ChangeRole(_alice, WorkspaceRole.Admin, Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(2), workspace.UpdatedAt);

        workspace.RemoveMember(_alice, Now.AddMinutes(3));
        Assert.Equal(Now.AddMinutes(3), workspace.UpdatedAt);
        Assert.Null(workspace.FindMember(_alice));
    }

    private static void AssertRule(string code, Action action) =>
        Assert.Equal(code, Assert.Throws<DomainException>(action).Code);
}

public sealed class WorkspaceInvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static WorkspaceInvitation NewInvitation(string email = "Sara@Example.com") =>
        WorkspaceInvitation.Create(Guid.CreateVersion7(), email, WorkspaceRole.Member, "hash", Guid.CreateVersion7(), Now, TimeSpan.FromDays(7));

    [Fact]
    public void Status_moves_from_pending_to_expired_with_time()
    {
        var invitation = NewInvitation();

        Assert.Equal("sara@example.com", invitation.Email);
        Assert.Equal(InvitationStatus.Pending, invitation.StatusAt(Now.AddDays(6)));
        Assert.Equal(InvitationStatus.Expired, invitation.StatusAt(Now.AddDays(7)));
    }

    [Fact]
    public void Accept_requires_the_invited_email_in_any_case()
    {
        var invitation = NewInvitation();

        Assert.Equal(InvitationErrors.EmailMismatch, Assert.Throws<DomainException>(() => invitation.Accept(Guid.CreateVersion7(), "other@example.com", Now)).Code);

        invitation.Accept(Guid.CreateVersion7(), "SARA@example.com", Now.AddHours(1));
        Assert.Equal(InvitationStatus.Accepted, invitation.StatusAt(Now.AddHours(2)));
    }

    [Fact]
    public void Only_pending_invitations_can_be_accepted_or_revoked()
    {
        var expired = NewInvitation();
        var revoked = NewInvitation();
        revoked.Revoke(Now);

        Assert.Equal("invitation.expired", Assert.Throws<DomainException>(() => expired.Accept(Guid.CreateVersion7(), "sara@example.com", Now.AddDays(8))).Code);
        Assert.Equal("invitation.revoked", Assert.Throws<DomainException>(() => revoked.Accept(Guid.CreateVersion7(), "sara@example.com", Now)).Code);
        Assert.Equal("invitation.revoked", Assert.Throws<DomainException>(() => revoked.Revoke(Now)).Code);
    }

    [Fact]
    public void Invitations_cannot_grant_ownership() =>
        Assert.Throws<DomainException>(() =>
            WorkspaceInvitation.Create(Guid.CreateVersion7(), "a@b.co", WorkspaceRole.Owner, "hash", Guid.CreateVersion7(), Now, TimeSpan.FromDays(1)));
}
