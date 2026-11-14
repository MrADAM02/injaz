using Injaz.Domain.Users;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Persistence;

/// <summary>
/// The application's view of the database. Handlers query and track entities through these sets but never call
/// <see cref="IUnitOfWork.SaveChangesAsync"/> — the dispatcher commits once per command.
/// </summary>
public interface IAppDbContext : IUnitOfWork
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Workspace> Workspaces { get; }

    /// <summary>Read side of <see cref="Workspace.Members"/>. Change memberships through the aggregate.</summary>
    DbSet<WorkspaceMember> WorkspaceMembers { get; }

    DbSet<WorkspaceInvitation> WorkspaceInvitations { get; }
}
