using Injaz.Domain.Common;

namespace Injaz.Domain.Workspaces;

/// <summary>
/// Aggregate root for a workspace and its memberships. Invariant: exactly one <see cref="WorkspaceRole.Owner"/>.
/// Every membership change also touches <see cref="UpdatedAt"/>, so the workspace row's concurrency token (xmin)
/// changes too — two admins editing members at the same time can't both succeed on a stale view.
/// </summary>
public sealed class Workspace : Entity
{
    public const int NameMaxLength = 100;

    private readonly List<WorkspaceMember> _members = [];

    private Workspace()
    {
        // EF Core
    }

    public string Name { get; private set; } = null!;

    public Guid CreatedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<WorkspaceMember> Members => _members.AsReadOnly();

    public static Workspace Create(string name, Guid ownerId, DateTimeOffset now)
    {
        var workspace = new Workspace
        {
            Name = name.Trim(),
            CreatedById = ownerId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        workspace._members.Add(new WorkspaceMember(workspace.Id, ownerId, WorkspaceRole.Owner, now));
        return workspace;
    }

    public WorkspaceMember? FindMember(Guid userId) => _members.Find(m => m.UserId == userId);

    public WorkspaceMember Owner => _members.Single(m => m.Role == WorkspaceRole.Owner);

    public void Rename(string name, DateTimeOffset now)
    {
        Name = name.Trim();
        UpdatedAt = now;
    }

    public WorkspaceMember AddMember(Guid userId, WorkspaceRole role, DateTimeOffset now)
    {
        EnsureNotOwnerRole(role);

        if (FindMember(userId) is not null)
        {
            throw new DomainException(WorkspaceErrors.AlreadyMember, "The user is already a member of this workspace.");
        }

        var member = new WorkspaceMember(Id, userId, role, now);
        _members.Add(member);
        UpdatedAt = now;
        return member;
    }

    /// <summary>Changes a non-owner's role. Ownership moves only through <see cref="TransferOwnership"/>.</summary>
    public WorkspaceMember ChangeRole(Guid userId, WorkspaceRole role, DateTimeOffset now)
    {
        EnsureNotOwnerRole(role);
        var member = GetMember(userId);

        if (member.Role == WorkspaceRole.Owner)
        {
            throw new DomainException(WorkspaceErrors.CannotChangeOwnerRole, "The owner's role can't be changed; transfer ownership instead.");
        }

        member.Role = role;
        UpdatedAt = now;
        return member;
    }

    public void RemoveMember(Guid userId, DateTimeOffset now)
    {
        var member = GetMember(userId);

        if (member.Role == WorkspaceRole.Owner)
        {
            throw new DomainException(WorkspaceErrors.CannotRemoveOwner, "The owner can't leave or be removed; transfer ownership first.");
        }

        _members.Remove(member);
        UpdatedAt = now;
    }

    /// <summary>The new owner must already be a member; the previous owner stays on as an admin.</summary>
    public void TransferOwnership(Guid newOwnerId, DateTimeOffset now)
    {
        var newOwner = GetMember(newOwnerId);
        var currentOwner = Owner;

        if (newOwner.UserId == currentOwner.UserId)
        {
            throw new DomainException(WorkspaceErrors.AlreadyOwner, "This member already owns the workspace.");
        }

        currentOwner.Role = WorkspaceRole.Admin;
        newOwner.Role = WorkspaceRole.Owner;
        UpdatedAt = now;
    }

    private WorkspaceMember GetMember(Guid userId) =>
        FindMember(userId) ?? throw new DomainException(WorkspaceErrors.MemberNotFound, "The user is not a member of this workspace.");

    private static void EnsureNotOwnerRole(WorkspaceRole role)
    {
        if (role == WorkspaceRole.Owner)
        {
            throw new DomainException(WorkspaceErrors.OwnerRoleNotAssignable, "The owner role can only be given by transferring ownership.");
        }
    }
}

public static class WorkspaceErrors
{
    public const string AlreadyMember = "workspace.alreadyMember";
    public const string AlreadyOwner = "workspace.alreadyOwner";
    public const string MemberNotFound = "workspace.memberNotFound";
    public const string CannotChangeOwnerRole = "workspace.cannotChangeOwnerRole";
    public const string CannotRemoveOwner = "workspace.cannotRemoveOwner";
    public const string OwnerRoleNotAssignable = "workspace.ownerRoleNotAssignable";
}
