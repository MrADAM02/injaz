using Injaz.Application.Common.Exceptions;
using Injaz.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Injaz.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
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
