using Injaz.Application.Features.Profile;

namespace Injaz.Api.Auth;

/// <summary>Body returned whenever a session starts or is refreshed. The refresh token is only ever in the cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserDto User)
{
    public override string ToString() => $"AuthResponse {{ UserId = {User.Id} }}";
}
