using Injaz.Domain.Workspaces;

namespace Injaz.Application.Security;

/// <summary>What a request needs to do. Each permission maps to the minimum workspace role (see the README matrix).</summary>
public enum Permission
{
    /// <summary>Read the workspace, its boards, cards and activity.</summary>
    View,

    /// <summary>Create and edit lists, cards, labels, checklists and comments; create/rename/archive boards.</summary>
    EditContent,

    /// <summary>Hard-delete boards, lists and cards; delete other people's comments.</summary>
    DeleteContent,

    /// <summary>Invite, re-role and remove members.</summary>
    ManageMembers,

    /// <summary>Rename the workspace.</summary>
    ManageWorkspace,

    /// <summary>Delete the workspace or transfer ownership.</summary>
    OwnWorkspace,
}

public static class Permissions
{
    public static WorkspaceRole MinimumRole(Permission permission) => permission switch
    {
        Permission.View => WorkspaceRole.Viewer,
        Permission.EditContent => WorkspaceRole.Member,
        Permission.DeleteContent => WorkspaceRole.Admin,
        Permission.ManageMembers => WorkspaceRole.Admin,
        Permission.ManageWorkspace => WorkspaceRole.Admin,
        Permission.OwnWorkspace => WorkspaceRole.Owner,
        _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null),
    };

    public static bool Allows(this WorkspaceRole role, Permission permission) => role >= MinimumRole(permission);
}
