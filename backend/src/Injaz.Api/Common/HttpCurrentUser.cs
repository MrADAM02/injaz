using System.Security.Claims;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Security;

namespace Injaz.Api.Common;

/// <summary>Reads the caller from the authenticated principal (the JWT <c>sub</c> claim, wired up in phase 2).</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated => TryGetUserId(out _);

    public Guid UserId => TryGetUserId(out var id) ? id : throw new UnauthorizedException();

    private bool TryGetUserId(out Guid userId)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var subject = principal?.FindFirstValue("sub") ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId) && principal?.Identity?.IsAuthenticated == true;
    }
}
