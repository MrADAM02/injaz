using Injaz.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Security;

public interface IAccessScopeResolver
{
    /// <returns>The caller's membership, or null if the resource doesn't exist or the caller isn't a member.</returns>
    Task<WorkspaceAccess?> ResolveAsync(ResourceScope scope, Guid userId, CancellationToken cancellationToken);
}

internal sealed class AccessScopeResolver(IAppDbContext db) : IAccessScopeResolver
{
    public Task<WorkspaceAccess?> ResolveAsync(ResourceScope scope, Guid userId, CancellationToken cancellationToken) =>
        scope.Kind switch
        {
            ScopeKind.Workspace => db.WorkspaceMembers
                .AsNoTracking()
                .Where(m => m.WorkspaceId == scope.Id && m.UserId == userId)
                .Select(m => new WorkspaceAccess(m.WorkspaceId, m.UserId, m.Role))
                .SingleOrDefaultAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope.Kind, "Unsupported scope kind."),
        };
}

internal sealed class CurrentAccess : ICurrentAccess
{
    private WorkspaceAccess? _access;

    public WorkspaceAccess Access =>
        _access ?? throw new InvalidOperationException($"No workspace access was resolved; the request must implement {nameof(IAuthorizedRequest)}.");

    internal void Set(WorkspaceAccess access) => _access = access;
}
