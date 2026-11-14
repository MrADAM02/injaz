using Injaz.Domain.Common;
using Injaz.Domain.Users;

namespace Injaz.Domain.Workspaces;

/// <summary>
/// An invitation for a specific email address to join a workspace with a given role. The link carries a random
/// secret; only its hash is stored. It can be accepted once, only by the account with the invited email.
/// </summary>
public sealed class WorkspaceInvitation : Entity
{
    private WorkspaceInvitation()
    {
        // EF Core
    }

    public Guid WorkspaceId { get; private set; }

    /// <summary>Normalized like <see cref="User.Email"/>.</summary>
    public string Email { get; private set; } = null!;

    public WorkspaceRole Role { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public Guid InvitedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static WorkspaceInvitation Create(
        Guid workspaceId, string email, WorkspaceRole role, string tokenHash, Guid invitedById, DateTimeOffset now, TimeSpan lifetime)
    {
        if (role == WorkspaceRole.Owner)
        {
            throw new DomainException(WorkspaceErrors.OwnerRoleNotAssignable, "Invitations can't grant the owner role.");
        }

        return new WorkspaceInvitation
        {
            WorkspaceId = workspaceId,
            Email = User.NormalizeEmail(email),
            Role = role,
            TokenHash = tokenHash,
            InvitedById = invitedById,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };
    }

    public InvitationStatus StatusAt(DateTimeOffset now) =>
        AcceptedAt is not null ? InvitationStatus.Accepted
        : RevokedAt is not null ? InvitationStatus.Revoked
        : now >= ExpiresAt ? InvitationStatus.Expired
        : InvitationStatus.Pending;

    public void Accept(Guid userId, string userEmail, DateTimeOffset now)
    {
        EnsurePending(now);

        if (!string.Equals(Email, User.NormalizeEmail(userEmail), StringComparison.Ordinal))
        {
            throw new DomainException(InvitationErrors.EmailMismatch, "This invitation was sent to a different email address.");
        }

        AcceptedAt = now;
        AcceptedByUserId = userId;
    }

    public void Revoke(DateTimeOffset now)
    {
        EnsurePending(now);
        RevokedAt = now;
    }

    private void EnsurePending(DateTimeOffset now)
    {
        var status = StatusAt(now);
        if (status != InvitationStatus.Pending)
        {
            throw new DomainException(InvitationErrors.NotPending(status), $"The invitation is {status.ToString().ToLowerInvariant()}.");
        }
    }
}

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2,
    Expired = 3,
}

public static class InvitationErrors
{
    public const string EmailMismatch = "invitation.emailMismatch";

    /// <summary><c>invitation.accepted</c>, <c>invitation.revoked</c> or <c>invitation.expired</c>.</summary>
    public static string NotPending(InvitationStatus status) => $"invitation.{status.ToString().ToLowerInvariant()}";
}
