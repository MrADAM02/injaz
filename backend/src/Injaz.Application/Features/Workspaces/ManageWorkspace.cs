using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Features.Workspaces;

// ---------- rename (Admin+) ----------

public sealed record RenameWorkspace(Guid WorkspaceId, string? Name) : ICommand<WorkspaceDto>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.ManageWorkspace;
}

internal sealed class RenameWorkspaceValidator : IValidator<RenameWorkspace>
{
    public void Validate(RenameWorkspace request, ValidationContext context) =>
        WorkspaceRules.Name(context, nameof(request.Name), request.Name);
}

internal sealed class RenameWorkspaceHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<RenameWorkspace, WorkspaceDto>
{
    public async Task<WorkspaceDto> HandleAsync(RenameWorkspace command, CancellationToken cancellationToken)
    {
        var workspace = await db.LoadWorkspaceAsync(command.WorkspaceId, cancellationToken);
        workspace.Rename(command.Name!, clock.GetUtcNow());
        return WorkspaceDto.From(workspace, currentUser.UserId);
    }
}

// ---------- delete (Owner) ----------

/// <summary>Permanently deletes the workspace and everything in it (members, invitations, and later boards).</summary>
public sealed record DeleteWorkspace(Guid WorkspaceId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.OwnWorkspace;
}

internal sealed class DeleteWorkspaceHandler(IAppDbContext db, Realtime.IBoardEvents events) : ICommandHandler<DeleteWorkspace, Unit>
{
    public async Task<Unit> HandleAsync(DeleteWorkspace command, CancellationToken cancellationToken)
    {
        var workspace = await db.LoadWorkspaceAsync(command.WorkspaceId, cancellationToken);
        db.Workspaces.Remove(workspace);

        // Boards are removed by cascade; tell anyone watching them (ids must be read before the commit).
        var boardIds = await db.Boards.Where(b => b.WorkspaceId == workspace.Id).Select(b => b.Id).ToListAsync(cancellationToken);
        foreach (var boardId in boardIds)
        {
            events.BoardDeleted(boardId);
        }
        return Unit.Value;
    }
}

// ---------- transfer ownership (Owner) ----------

/// <summary>Makes another member the owner; the current owner becomes an admin.</summary>
public sealed record TransferOwnership(Guid WorkspaceId, Guid NewOwnerId) : ICommand, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.OwnWorkspace;
}

internal sealed class TransferOwnershipValidator : IValidator<TransferOwnership>
{
    public void Validate(TransferOwnership request, ValidationContext context) =>
        context.RuleFor(nameof(request.NewOwnerId), request.NewOwnerId).NotEmpty();
}

internal sealed class TransferOwnershipHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<TransferOwnership, Unit>
{
    public async Task<Unit> HandleAsync(TransferOwnership command, CancellationToken cancellationToken)
    {
        var workspace = await db.LoadWorkspaceAsync(command.WorkspaceId, cancellationToken);
        if (workspace.FindMember(command.NewOwnerId) is null)
        {
            throw new Common.Exceptions.NotFoundException("member");
        }

        workspace.TransferOwnership(command.NewOwnerId, clock.GetUtcNow());
        return Unit.Value;
    }
}
