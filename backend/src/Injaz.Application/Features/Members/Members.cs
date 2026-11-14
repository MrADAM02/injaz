using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Workspaces;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Realtime;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Members;

public sealed record MemberDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    WorkspaceRole Role,
    DateTimeOffset JoinedAt);

// ---------- list (any member) ----------

/// <summary>Members ordered by role (owner first), then name. Bounded per workspace, so not paginated.</summary>
public sealed record ListMembers(Guid WorkspaceId) : IQuery<IReadOnlyList<MemberDto>>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.View;
}

internal sealed class ListMembersHandler(IAppDbContext db) : IQueryHandler<ListMembers, IReadOnlyList<MemberDto>>
{
    public async Task<IReadOnlyList<MemberDto>> HandleAsync(ListMembers query, CancellationToken cancellationToken) =>
        await db.WorkspaceMembers
            .AsNoTracking()
            .Where(m => m.WorkspaceId == query.WorkspaceId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { Member = m, User = u })
            .OrderByDescending(x => x.Member.Role)
            .ThenBy(x => x.User.DisplayName)
            .Select(x => new MemberDto(x.User.Id, x.User.DisplayName, x.User.Email, x.User.AvatarUrl, x.Member.Role, x.Member.JoinedAt))
            .ToListAsync(cancellationToken);
}

// ---------- change role (Admin+) ----------

/// <summary>Admins and the owner can give any non-owner any role except owner (that's a transfer).</summary>
public sealed record ChangeMemberRole(Guid WorkspaceId, Guid UserId, WorkspaceRole? Role) : ICommand<MemberDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.ManageMembers;
}

internal sealed class ChangeMemberRoleValidator : IValidator<ChangeMemberRole>
{
    public void Validate(ChangeMemberRole request, ValidationContext context) =>
        context.RuleFor(nameof(request.Role), request.Role).NotNull().IsInEnum();
}

internal sealed class ChangeMemberRoleHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<ChangeMemberRole, MemberDto>
{
    public async Task<MemberDto> HandleAsync(ChangeMemberRole command, CancellationToken cancellationToken)
    {
        var workspace = await db.LoadWorkspaceAsync(command.WorkspaceId, cancellationToken);
        if (workspace.FindMember(command.UserId) is null)
        {
            throw new NotFoundException("member");
        }

        var member = workspace.ChangeRole(command.UserId, command.Role!.Value, clock.GetUtcNow());
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == member.UserId, cancellationToken);
        return new MemberDto(user.Id, user.DisplayName, user.Email, user.AvatarUrl, member.Role, member.JoinedAt);
    }
}

// ---------- remove / leave ----------

/// <summary>
/// Removing yourself (leaving) needs only membership; removing someone else needs <see cref="Permission.ManageMembers"/>.
/// The declared permission is therefore the lower one, and the handler enforces the higher one when it applies.
/// </summary>
public sealed record RemoveMember(Guid WorkspaceId, Guid UserId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.View;
}

internal sealed class RemoveMemberHandler(IAppDbContext db, ICurrentAccess currentAccess, IBoardEvents events, TimeProvider clock)
    : ICommandHandler<RemoveMember, Unit>
{
    public async Task<Unit> HandleAsync(RemoveMember command, CancellationToken cancellationToken)
    {
        var access = currentAccess.Access;
        var leaving = command.UserId == access.UserId;

        if (!leaving && !access.Role.Allows(Permission.ManageMembers))
        {
            throw new ForbiddenException();
        }

        var workspace = await db.LoadWorkspaceAsync(command.WorkspaceId, cancellationToken);
        if (workspace.FindMember(command.UserId) is null)
        {
            throw new NotFoundException("member");
        }

        var now = clock.GetUtcNow();
        workspace.RemoveMember(command.UserId, now);

        // A former member must not stay assigned to cards they can no longer see.
        var assignedCards = await db.Cards
            .Include(c => c.Assignees)
            .Where(c => c.Assignees.Any(a => a.UserId == command.UserId))
            .Join(db.Boards.Where(b => b.WorkspaceId == command.WorkspaceId), c => c.BoardId, b => b.Id, (c, _) => c)
            .ToListAsync(cancellationToken);

        foreach (var card in assignedCards)
        {
            card.Unassign(command.UserId, now);
            events.CardChanged(card.BoardId, card.Id);
        }

        // Live connections of the removed member stop receiving this workspace's boards.
        events.AccessRevoked(command.WorkspaceId, command.UserId);
        return Unit.Value;
    }
}
