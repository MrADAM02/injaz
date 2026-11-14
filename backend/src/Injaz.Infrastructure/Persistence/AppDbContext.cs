using Injaz.Application.Common.Exceptions;
using Injaz.Application.Persistence;
using Injaz.Domain.Activity;
using Injaz.Domain.Boards;
using Injaz.Domain.Cards;
using Injaz.Domain.Users;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Injaz.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();

    public DbSet<Board> Boards => Set<Board>();

    public DbSet<BoardList> BoardLists => Set<BoardList>();

    public DbSet<Label> Labels => Set<Label>();

    public DbSet<Card> Cards => Set<Card>();

    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<ActivityEntry> Activity => Set<ActivityEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Entity configurations live in Persistence/Configurations (one IEntityTypeConfiguration per entity).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>Translates persistence failures that callers can act on into application exceptions (→ 409).</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException(
                "conflict.concurrency",
                "The resource was modified by someone else. Reload it and try again.",
                ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("conflict.duplicate", "A resource with the same unique value already exists.", ex);
        }
    }
}
