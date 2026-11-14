using Injaz.Application.Common.Exceptions;
using Injaz.Application.Persistence;
using Injaz.Application.Validation;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Workspaces;

/// <summary>A workspace as seen by the caller (includes the caller's own role).</summary>
public sealed record WorkspaceDto(Guid Id, string Name, WorkspaceRole MyRole, int MemberCount, DateTimeOffset CreatedAt)
{
    /// <summary>Builds the DTO from a loaded aggregate (members included) — used by commands before commit.</summary>
    public static WorkspaceDto From(Workspace workspace, Guid callerId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var role = workspace.FindMember(callerId)?.Role
            ?? throw new InvalidOperationException("The caller is not a member of this workspace.");
        return new(workspace.Id, workspace.Name, role, workspace.Members.Count, workspace.CreatedAt);
    }
}

internal static class WorkspaceQueries
{
    /// <summary>Loads the aggregate with its members for a state change. Authorization has already run.</summary>
    public static async Task<Workspace> LoadWorkspaceAsync(this IAppDbContext db, Guid workspaceId, CancellationToken cancellationToken) =>
        await db.Workspaces.Include(w => w.Members).SingleOrDefaultAsync(w => w.Id == workspaceId, cancellationToken)
        ?? throw new NotFoundException("workspace");

    /// <summary>Read-side projection for the caller.</summary>
    public static IQueryable<WorkspaceDto> SelectDto(this IQueryable<Workspace> workspaces, Guid callerId) =>
        workspaces.Select(w => new WorkspaceDto(
            w.Id,
            w.Name,
            w.Members.Where(m => m.UserId == callerId).Select(m => m.Role).First(),
            w.Members.Count,
            w.CreatedAt));
}

internal static class WorkspaceRules
{
    public static void Name(ValidationContext context, string field, string? name) =>
        context.RuleFor(field, name).NotEmpty().MaxLength(Workspace.NameMaxLength);
}
