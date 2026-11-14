using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Workspaces;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Invitations;

// ---------- preview (public: whoever holds the secret link) ----------

public sealed record GetInvitation(string? Token) : IQuery<InvitationPreviewDto>, IPublicRequest
{
    public override string ToString() => nameof(GetInvitation);
}

internal sealed class GetInvitationHandler(IAppDbContext db, TimeProvider clock) : IQueryHandler<GetInvitation, InvitationPreviewDto>
{
    public async Task<InvitationPreviewDto> HandleAsync(GetInvitation query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Token))
        {
            throw new NotFoundException("invitation");
        }

        var hash = SecureToken.Hash(query.Token);
        var row = await db.WorkspaceInvitations
            .AsNoTracking()
            .Where(i => i.TokenHash == hash)
            .Join(db.Workspaces, i => i.WorkspaceId, w => w.Id, (i, w) => new { Invitation = i, WorkspaceName = w.Name })
            .Join(db.Users, x => x.Invitation.InvitedById, u => u.Id, (x, u) => new { x.Invitation, x.WorkspaceName, InviterName = u.DisplayName })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("invitation");

        var invitation = row.Invitation;
        return new InvitationPreviewDto(
            invitation.WorkspaceId,
            row.WorkspaceName,
            row.InviterName,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAt,
            invitation.StatusAt(clock.GetUtcNow()));
    }
}

// ---------- accept (signed in, email must match) ----------

public sealed record AcceptInvitation(string? Token) : ICommand<WorkspaceDto>, IAuthenticatedRequest
{
    public override string ToString() => nameof(AcceptInvitation);
}

internal sealed class AcceptInvitationHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<AcceptInvitation, WorkspaceDto>
{
    public async Task<WorkspaceDto> HandleAsync(AcceptInvitation command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            throw new NotFoundException("invitation");
        }

        var hash = SecureToken.Hash(command.Token);
        var invitation = await db.WorkspaceInvitations.SingleOrDefaultAsync(i => i.TokenHash == hash, cancellationToken)
            ?? throw new NotFoundException("invitation");

        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == currentUser.UserId, cancellationToken);
        var workspace = await db.LoadWorkspaceAsync(invitation.WorkspaceId, cancellationToken);
        var now = clock.GetUtcNow();

        invitation.Accept(user.Id, user.Email, now);

        // Someone who joined by another route in the meantime simply keeps their current role.
        if (workspace.FindMember(user.Id) is null)
        {
            workspace.AddMember(user.Id, invitation.Role, now);
        }

        return WorkspaceDto.From(workspace, user.Id);
    }
}
