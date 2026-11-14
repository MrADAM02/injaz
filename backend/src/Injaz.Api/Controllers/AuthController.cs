using Injaz.Api.Auth;
using Injaz.Api.Common;
using Injaz.Application.Features.Auth;
using Injaz.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Injaz.Api.Controllers;

[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(IDispatcher dispatcher, RefreshTokenCookie refreshCookie) : ApiControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterAsync(Register command, CancellationToken cancellationToken)
    {
        var session = await dispatcher.SendAsync(command, cancellationToken);
        return Created("/api/me", StartSession(session));
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    public async Task<AuthResponse> LoginAsync(Login command, CancellationToken cancellationToken)
    {
        var session = await dispatcher.SendAsync(command, cancellationToken);
        return StartSession(session);
    }

    [HttpPost("refresh")]
    [RequireCsrfHeader]
    public async Task<AuthResponse> RefreshAsync(CancellationToken cancellationToken)
    {
        var session = await dispatcher.SendAsync(new RefreshSession(refreshCookie.Read(Request)), cancellationToken);
        return StartSession(session);
    }

    [HttpPost("logout")]
    [RequireCsrfHeader]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new Logout(refreshCookie.Read(Request)), cancellationToken);
        refreshCookie.Clear(Response);
        return NoContent();
    }

    private AuthResponse StartSession(AuthSession session)
    {
        refreshCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt);
        return new AuthResponse(session.AccessToken, session.AccessTokenExpiresAt, session.User);
    }
}
