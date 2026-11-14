using Injaz.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Security;

public interface IAccessScopeResolver
{
    /// <returns>The caller's membership, or null if the resource doesn't exist or the caller isn't a member.</returns>
    Task<WorkspaceAccess?> ResolveAsync(ResourceScope scope, Guid userId, CancellationToken cancellationToken);
}

/// <summary>One round trip per request: resource → workspace → the caller's membership, in a single SQL query.</summary>
internal sealed class AccessScopeResolver(IAppDbContext db) : IAccessScopeResolver
{
    public Task<WorkspaceAccess?> ResolveAsync(ResourceScope scope, Guid userId, CancellationToken cancellationToken)
    {
        var id = scope.Id;
        var workspaceIds = scope.Kind switch
        {
            ScopeKind.Workspace => db.Workspaces.Where(w => w.Id == id).Select(w => w.Id),
            ScopeKind.Board => db.Boards.Where(b => b.Id == id).Select(b => b.WorkspaceId),
            ScopeKind.List => db.BoardLists.Where(l => l.Id == id).Join(db.Boards, l => l.BoardId, b => b.Id, (_, b) => b.WorkspaceId),
            ScopeKind.Card => db.Cards.Where(c => c.Id == id).Join(db.Boards, c => c.BoardId, b => b.Id, (_, b) => b.WorkspaceId),
            ScopeKind.Label => db.Labels.Where(l => l.Id == id).Join(db.Boards, l => l.BoardId, b => b.Id, (_, b) => b.WorkspaceId),
            ScopeKind.ChecklistItem => db.ChecklistItems.Where(i => i.Id == id)
                .Join(db.Cards, i => i.CardId, c => c.Id, (_, c) => c.BoardId)
                .Join(db.Boards, boardId => boardId, b => b.Id, (_, b) => b.WorkspaceId),
            ScopeKind.Comment => db.Comments.Where(x => x.Id == id)
                .Join(db.Cards, x => x.CardId, c => c.Id, (_, c) => c.BoardId)
                .Join(db.Boards, boardId => boardId, b => b.Id, (_, b) => b.WorkspaceId),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope.Kind, "Unsupported scope kind."),
        };

        return db.WorkspaceMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && workspaceIds.Contains(m.WorkspaceId))
            .Select(m => new WorkspaceAccess(m.WorkspaceId, m.UserId, m.Role))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

internal sealed class CurrentAccess : ICurrentAccess
{
    private WorkspaceAccess? _access;

    public WorkspaceAccess Access =>
        _access ?? throw new InvalidOperationException($"No workspace access was resolved; the request must implement {nameof(IAuthorizedRequest)}.");

    internal void Set(WorkspaceAccess access) => _access = access;
}
