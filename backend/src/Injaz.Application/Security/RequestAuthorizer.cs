using Injaz.Application.Common.Exceptions;

namespace Injaz.Application.Security;

public interface IRequestAuthorizer
{
    Task AuthorizeAsync<TRequest>(TRequest request, CancellationToken cancellationToken)
        where TRequest : notnull;
}

internal sealed class RequestAuthorizer(
    ICurrentUser currentUser,
    IAccessScopeResolver scopeResolver,
    CurrentAccess currentAccess) : IRequestAuthorizer
{
    public async Task AuthorizeAsync<TRequest>(TRequest request, CancellationToken cancellationToken)
        where TRequest : notnull
    {
        switch (request)
        {
            case IPublicRequest:
                return;

            case IAuthenticatedRequest:
                EnsureAuthenticated();
                return;

            case IAuthorizedRequest authorized:
                EnsureAuthenticated();

                var access = await scopeResolver.ResolveAsync(authorized.Scope, currentUser.UserId, cancellationToken)
                    ?? throw new NotFoundException(authorized.Scope.ResourceName);

                if (!access.Role.Allows(authorized.Permission))
                {
                    throw new ForbiddenException();
                }

                currentAccess.Set(access);
                return;

            default:
                throw new InvalidOperationException(
                    $"Request '{typeof(TRequest).FullName}' does not declare an access policy. " +
                    $"Implement {nameof(IPublicRequest)}, {nameof(IAuthenticatedRequest)} or {nameof(IAuthorizedRequest)}.");
        }
    }

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException();
        }
    }
}
