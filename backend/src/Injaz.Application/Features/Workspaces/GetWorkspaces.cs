using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Workspaces;

/// <summary>All workspaces the caller belongs to, alphabetically. Bounded per user, so not paginated.</summary>
public sealed record ListMyWorkspaces : IQuery<IReadOnlyList<WorkspaceDto>>, IAuthenticatedRequest;

internal sealed class ListMyWorkspacesHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<ListMyWorkspaces, IReadOnlyList<WorkspaceDto>>
{
    public async Task<IReadOnlyList<WorkspaceDto>> HandleAsync(ListMyWorkspaces query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        return await db.Workspaces
            .AsNoTracking()
            .Where(w => w.Members.Any(m => m.UserId == userId))
            .OrderBy(w => w.Name)
            .ThenBy(w => w.Id)
            .SelectDto(userId)
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetWorkspace(Guid WorkspaceId) : IQuery<WorkspaceDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.View;
}

internal sealed class GetWorkspaceHandler(IAppDbContext db, ICurrentUser currentUser) : IQueryHandler<GetWorkspace, WorkspaceDto>
{
    public async Task<WorkspaceDto> HandleAsync(GetWorkspace query, CancellationToken cancellationToken) =>
        await db.Workspaces
            .AsNoTracking()
            .Where(w => w.Id == query.WorkspaceId)
            .SelectDto(currentUser.UserId)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("workspace");
}
