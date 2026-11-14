namespace Injaz.Domain.Workspaces;

/// <summary>Roles are ordered: each one includes every capability of the roles below it.</summary>
public enum WorkspaceRole
{
    Viewer = 0,
    Member = 1,
    Admin = 2,
    Owner = 3,
}
