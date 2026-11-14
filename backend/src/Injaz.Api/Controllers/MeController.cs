using Injaz.Api.Auth;
using Injaz.Api.Common;
using Injaz.Application.Features.Profile;
using Injaz.Application.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Injaz.Api.Controllers;

[Route("api/me")]
public sealed class MeController(IDispatcher dispatcher, RefreshTokenCookie refreshCookie) : ApiControllerBase
{
    [HttpGet]
    public Task<UserDto> GetAsync(CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new GetMe(), cancellationToken);

    [HttpPatch]
    public Task<UserDto> UpdateAsync(UpdateProfile command, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(command, cancellationToken);

    /// <summary>Changes the password, signs out all other sessions, and starts a fresh one for this device.</summary>
    [HttpPut("password")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<AuthResponse> ChangePasswordAsync(ChangePassword command, CancellationToken cancellationToken)
    {
        var session = await dispatcher.SendAsync(command, cancellationToken);
        refreshCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
        return new AuthResponse(session.AccessToken, session.AccessTokenExpiresAt, session.User);
    }
}
