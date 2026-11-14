using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Auth;

/// <summary>
/// Ends the session that owns <paramref name="RefreshToken"/> (its whole family). Public because the access token
/// may already be expired; idempotent so an unknown or missing token still "succeeds".
/// </summary>
public sealed record Logout(string? RefreshToken) : ICommand, IPublicRequest
{
    public override string ToString() => nameof(Logout);
}

internal sealed class LogoutHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<Logout, Unit>
{
    public async Task<Unit> HandleAsync(Logout command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Unit.Value;
        }

        var hash = RefreshTokenSecret.Hash(command.RefreshToken);
        var familyId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (familyId is null)
        {
            return Unit.Value;
        }

        var now = clock.GetUtcNow();
        var active = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.Revoke(now);
        }

        return Unit.Value;
    }
}
