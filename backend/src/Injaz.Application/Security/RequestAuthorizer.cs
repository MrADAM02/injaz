using Injaz.Application.Common.Exceptions;

namespace Injaz.Application.Security;

public interface IRequestAuthorizer
{
    Task AuthorizeAsync<TRequest>(TRequest request, CancellationToken cancellationToken)
        where TRequest : notnull;
}

internal sealed class RequestAuthorizer(ICurrentUser currentUser) : IRequestAuthorizer
{
    public Task AuthorizeAsync<TRequest>(TRequest request, CancellationToken cancellationToken)
        where TRequest : notnull
    {
        switch (request)
        {
            case IPublicRequest:
                return Task.CompletedTask;

            case IAuthenticatedRequest:
                if (!currentUser.IsAuthenticated)
                {
                    throw new UnauthorizedException();
                }

                return Task.CompletedTask;

            default:
                throw new InvalidOperationException(
                    $"Request '{typeof(TRequest).FullName}' does not declare an access policy. " +
                    $"Implement {nameof(IPublicRequest)} or {nameof(IAuthenticatedRequest)}.");
        }
    }
}
