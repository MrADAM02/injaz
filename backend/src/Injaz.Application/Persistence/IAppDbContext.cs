using Injaz.Domain.Users;
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
}
