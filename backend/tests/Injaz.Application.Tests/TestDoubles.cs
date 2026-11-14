using Injaz.Application.Common.Exceptions;
using Injaz.Application.Persistence;
using Injaz.Application.Security;

namespace Injaz.Application.Tests;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? Id { get; set; }

    public bool IsAuthenticated => Id.HasValue;

    public Guid UserId => Id ?? throw new UnauthorizedException();
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}

/// <summary>Records which handlers actually ran, so tests can assert that the pipeline short-circuited.</summary>
internal sealed class CallLog
{
    public List<string> Calls { get; } = [];
}
