using Injaz.Application.Common;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Injaz.Application.Features.Auth;

/// <summary>Exchanges a refresh token (from the httpOnly cookie) for a new access token and a rotated refresh token.</summary>
public sealed record RefreshSession(string? RefreshToken) : ICommand<AuthSession>, IPublicRequest
{
    public override string ToString() => nameof(RefreshSession);
}

internal sealed class RefreshSessionHandler(
    IAppDbContext db,
    SessionIssuer sessions,
    TimeProvider clock,
    IOptions<AuthOptions> options) : ICommandHandler<RefreshSession, AuthSession>
{
    public async Task<AuthSession> HandleAsync(RefreshSession command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        var hash = SecureToken.Hash(command.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw AuthErrors.InvalidRefreshToken();

        var now = clock.GetUtcNow();

        if (token.IsActive(now))
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId, cancellationToken)
                ?? throw AuthErrors.InvalidRefreshToken();

            // Concurrent rotation of the same token is caught by optimistic concurrency (xmin) → 409.
            return sessions.Rotate(user, token);
        }

        if (token.WasRotatedWithin(options.Value.RefreshReuseGracePeriod, now))
        {
            throw AuthErrors.RefreshSuperseded();
        }

        if (token.RevokedAt is not null)
        {
            // A revoked token came back outside the grace window: assume it was stolen and kill the whole family.
            // This must persist even though the request fails, so it bypasses the unit of work on purpose.
            await db.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
        }

        throw AuthErrors.InvalidRefreshToken();
    }
}
