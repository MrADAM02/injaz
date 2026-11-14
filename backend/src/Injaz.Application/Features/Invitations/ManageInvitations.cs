using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Invitations;

// ---------- list pending (Admin+) ----------

public sealed record ListInvitations(Guid WorkspaceId) : IQuery<IReadOnlyList<InvitationDto>>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.ManageMembers;
}

internal sealed class ListInvitationsHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<ListInvitations, IReadOnlyList<InvitationDto>>
{
    public async Task<IReadOnlyList<InvitationDto>> HandleAsync(ListInvitations query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await db.WorkspaceInvitations
            .AsNoTracking()
            .Where(i => i.WorkspaceId == query.WorkspaceId && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .Join(db.Users, i => i.InvitedById, u => u.Id, (i, u) => new { Invitation = i, Inviter = u })
            .OrderByDescending(x => x.Invitation.CreatedAt)
            .Select(x => new InvitationDto(
                x.Invitation.Id, x.Invitation.Email, x.Invitation.Role, x.Inviter.Id, x.Inviter.DisplayName, x.Invitation.CreatedAt, x.Invitation.ExpiresAt))
            .ToListAsync(cancellationToken);
    }
}

// ---------- revoke (Admin+) ----------

public sealed record RevokeInvitation(Guid WorkspaceId, Guid InvitationId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.ManageMembers;
}

internal sealed class RevokeInvitationHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<RevokeInvitation, Unit>
{
    public async Task<Unit> HandleAsync(RevokeInvitation command, CancellationToken cancellationToken)
    {
        // Scoped by workspace too, so an admin of one workspace can't touch another's invitations by id.
        var invitation = await db.WorkspaceInvitations
            .SingleOrDefaultAsync(i => i.Id == command.InvitationId && i.WorkspaceId == command.WorkspaceId, cancellationToken)
            ?? throw new NotFoundException("invitation");

        invitation.Revoke(clock.GetUtcNow());
        return Unit.Value;
    }
}
