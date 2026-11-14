namespace Injaz.Domain.Workspaces;

/// <summary>A user's membership in a workspace. Owned by the <see cref="Workspace"/> aggregate; key is (WorkspaceId, UserId).</summary>
public sealed class WorkspaceMember
{
    private WorkspaceMember()
    {
        // EF Core
    }

    internal WorkspaceMember(Guid workspaceId, Guid userId, WorkspaceRole role, DateTimeOffset joinedAt)
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; internal set; }

    public DateTimeOffset JoinedAt { get; private set; }
}
