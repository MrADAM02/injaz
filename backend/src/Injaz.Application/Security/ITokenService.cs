using Injaz.Domain.Users;

namespace Injaz.Application.Security;

/// <summary>Issues short-lived access tokens (JWT). Refresh tokens are opaque and handled in the Auth feature.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt)
{
    public override string ToString() => $"AccessToken {{ ExpiresAt = {ExpiresAt:O} }}";
}
