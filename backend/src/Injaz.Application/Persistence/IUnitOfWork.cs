namespace Injaz.Application.Persistence;

/// <summary>Commits all tracked changes atomically. Called only by the dispatcher after a command handler succeeds.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
