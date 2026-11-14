namespace Injaz.Application.Security;

public enum ScopeKind
{
    Workspace,
    // Phase 4+: Board, List, Card, Label, ChecklistItem, Comment — each resolves to its workspace.
}

/// <summary>The resource a request targets. The authorizer resolves it to a workspace and the caller's role there.</summary>
public readonly record struct ResourceScope(ScopeKind Kind, Guid Id)
{
    public static ResourceScope Workspace(Guid workspaceId) => new(ScopeKind.Workspace, workspaceId);

    /// <summary>Resource name used in <c>{resource}.notFound</c> codes.</summary>
    public string ResourceName => Kind switch
    {
        ScopeKind.Workspace => "workspace",
        _ => Kind.ToString().ToLowerInvariant(),
    };
}

/// <summary>The caller's membership in the workspace a request was authorized against.</summary>
public sealed record WorkspaceAccess(Guid WorkspaceId, Guid UserId, Domain.Workspaces.WorkspaceRole Role);
