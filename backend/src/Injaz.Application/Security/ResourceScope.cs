namespace Injaz.Application.Security;

/// <summary>Kinds of resources a request can target. Each resolves to exactly one workspace.</summary>
public enum ScopeKind
{
    Workspace,
    Board,
    List,
    Card,
    Label,
    ChecklistItem,
    Comment,
}

/// <summary>The resource a request targets. The authorizer resolves it to a workspace and the caller's role there.</summary>
public readonly record struct ResourceScope(ScopeKind Kind, Guid Id)
{
    public static ResourceScope Workspace(Guid workspaceId) => new(ScopeKind.Workspace, workspaceId);

    public static ResourceScope Board(Guid boardId) => new(ScopeKind.Board, boardId);

    public static ResourceScope List(Guid listId) => new(ScopeKind.List, listId);

    public static ResourceScope Card(Guid cardId) => new(ScopeKind.Card, cardId);

    public static ResourceScope Label(Guid labelId) => new(ScopeKind.Label, labelId);

    public static ResourceScope ChecklistItem(Guid itemId) => new(ScopeKind.ChecklistItem, itemId);

    public static ResourceScope Comment(Guid commentId) => new(ScopeKind.Comment, commentId);

    /// <summary>Resource name used in <c>{resource}.notFound</c> codes.</summary>
    public string ResourceName => Kind switch
    {
        ScopeKind.ChecklistItem => "checklistItem",
        _ => Kind.ToString().ToLowerInvariant(),
    };
}

/// <summary>The caller's membership in the workspace a request was authorized against.</summary>
public sealed record WorkspaceAccess(Guid WorkspaceId, Guid UserId, Domain.Workspaces.WorkspaceRole Role);
