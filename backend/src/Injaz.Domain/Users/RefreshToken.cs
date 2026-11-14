using Injaz.Domain.Common;

namespace Injaz.Domain.Users;

/// <summary>
/// One link in a rotating refresh-token chain. A login starts a new <see cref="FamilyId"/>; every refresh revokes
/// the presented token and issues its successor in the same family. Presenting an already-rotated token is a
/// strong signal the token leaked, so the whole family is revoked. Only a SHA-256 hash of the secret is stored.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
        // EF Core
    }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Set when this token was revoked by rotation (as opposed to logout or reuse detection).</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken StartFamily(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        Create(userId, Guid.CreateVersion7(), tokenHash, now, lifetime);

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>True when this token was rotated within <paramref name="gracePeriod"/> — typically two browser tabs refreshing at once.</summary>
    public bool WasRotatedWithin(TimeSpan gracePeriod, DateTimeOffset now) =>
        ReplacedByTokenId is not null && RevokedAt is { } revokedAt && now - revokedAt <= gracePeriod;

    /// <summary>Revokes this token and returns its successor in the same family.</summary>
    public RefreshToken Rotate(string newTokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (!IsActive(now))
        {
            throw new DomainException("auth.refreshTokenInactive", "Only an active refresh token can be rotated.");
        }

        var successor = Create(UserId, FamilyId, newTokenHash, now, lifetime);
        RevokedAt = now;
        ReplacedByTokenId = successor.Id;
        return successor;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    private static RefreshToken Create(Guid userId, Guid familyId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new()
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };
}
