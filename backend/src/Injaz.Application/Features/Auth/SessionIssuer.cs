using Injaz.Application.Common;
using Injaz.Application.Features.Profile;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Domain.Users;
using Microsoft.Extensions.Options;

namespace Injaz.Application.Features.Auth;

/// <summary>
/// Result of every use case that signs a user in. The API returns the access token in the body and moves the
/// refresh token into an httpOnly cookie — it never reaches JavaScript.
/// </summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User)
{
    // Never let secrets end up in logs via the compiler-generated ToString.
    public override string ToString() => $"AuthSession {{ UserId = {User.Id} }}";
}

internal sealed class SessionIssuer(
    IAppDbContext db,
    ITokenService tokenService,
    TimeProvider clock,
    IOptions<AuthOptions> options)
{
    /// <summary>Starts a new refresh-token family (login, registration, password change).</summary>
    public AuthSession Start(User user)
    {
        var (secret, hash) = SecureToken.Generate();
        var token = RefreshToken.StartFamily(user.Id, hash, clock.GetUtcNow(), options.Value.RefreshTokenLifetime);
        db.RefreshTokens.Add(token);
        return Build(user, secret, token);
    }

    /// <summary>Rotates <paramref name="current"/> and continues its family.</summary>
    public AuthSession Rotate(User user, RefreshToken current)
    {
        var (secret, hash) = SecureToken.Generate();
        var successor = current.Rotate(hash, clock.GetUtcNow(), options.Value.RefreshTokenLifetime);
        db.RefreshTokens.Add(successor);
        return Build(user, secret, successor);
    }

    private AuthSession Build(User user, string refreshSecret, RefreshToken refreshToken)
    {
        var access = tokenService.CreateAccessToken(user);
        return new AuthSession(access.Token, access.ExpiresAt, refreshSecret, refreshToken.ExpiresAt, UserDto.From(user));
    }
}
