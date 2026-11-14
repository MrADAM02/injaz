using Injaz.Application.Common;
using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Notifications;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Users;
using Injaz.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Injaz.Application.Features.Invitations;

/// <summary>
/// Invites an email address. Inviting the same address again revokes the previous pending invitation, so there
/// is only ever one live link per person. The link is returned to the inviter and also emailed after commit.
/// </summary>
public sealed record CreateInvitation(Guid WorkspaceId, string? Email, WorkspaceRole? Role)
    : ICommand<CreatedInvitationDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.ManageMembers;
}

internal sealed class CreateInvitationValidator : IValidator<CreateInvitation>
{
    public void Validate(CreateInvitation request, ValidationContext context)
    {
        context.RuleFor(nameof(request.Email), request.Email).NotEmpty().MaxLength(User.EmailMaxLength).EmailAddress();
        context.RuleFor(nameof(request.Role), request.Role)
            .NotNull()
            .IsInEnum()
            .Must(r => r != WorkspaceRole.Owner, ValidationCodes.InvalidValue);
    }
}

internal sealed class CreateInvitationHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IEmailSender emailSender,
    IAfterCommit afterCommit,
    IOptions<InvitationOptions> options,
    TimeProvider clock) : ICommandHandler<CreateInvitation, CreatedInvitationDto>
{
    public async Task<CreatedInvitationDto> HandleAsync(CreateInvitation command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email!);
        var now = clock.GetUtcNow();

        var alreadyMember = await db.WorkspaceMembers
            .Where(m => m.WorkspaceId == command.WorkspaceId)
            .Join(db.Users, m => m.UserId, u => u.Id, (_, u) => u.Email)
            .AnyAsync(e => e == email, cancellationToken);
        if (alreadyMember)
        {
            throw new ConflictException(WorkspaceErrors.AlreadyMember, "This person is already a member of the workspace.");
        }

        var previous = await db.WorkspaceInvitations
            .Where(i => i.WorkspaceId == command.WorkspaceId && i.Email == email
                && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var invitation in previous)
        {
            invitation.Revoke(now);
        }

        var (token, hash) = SecureToken.Generate();
        var created = WorkspaceInvitation.Create(
            command.WorkspaceId, email, command.Role!.Value, hash, currentUser.UserId, now, options.Value.Lifetime);
        db.WorkspaceInvitations.Add(created);

        var inviter = await db.Users.AsNoTracking().SingleAsync(u => u.Id == currentUser.UserId, cancellationToken);
        var workspaceName = await db.Workspaces.Where(w => w.Id == command.WorkspaceId).Select(w => w.Name).SingleAsync(cancellationToken);
        var acceptUrl = options.Value.AcceptUrl(token);

        afterCommit.Enqueue(ct => emailSender.SendAsync(InvitationEmail.Compose(created.Email, inviter.DisplayName, workspaceName, acceptUrl), ct));

        var dto = new InvitationDto(created.Id, created.Email, created.Role, inviter.Id, inviter.DisplayName, created.CreatedAt, created.ExpiresAt);
        return new CreatedInvitationDto(dto, token, acceptUrl);
    }
}

internal static class InvitationEmail
{
    // Bilingual: the invitee's language preference is unknown until they have an account.
    public static EmailMessage Compose(string to, string inviterName, string workspaceName, string acceptUrl) =>
        new(
            to,
            $"دعوة للانضمام إلى {workspaceName} · Invitation to join {workspaceName}",
            $"""
            دعاك {inviterName} للانضمام إلى مساحة العمل "{workspaceName}" على إنجاز.
            لقبول الدعوة افتح الرابط التالي:
            {acceptUrl}

            {inviterName} invited you to join the "{workspaceName}" workspace on Injaz.
            Open this link to accept:
            {acceptUrl}
            """);
}
