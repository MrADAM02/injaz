using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Workspaces;

namespace Injaz.Application.Features.Workspaces;

/// <summary>Any signed-in user can create a workspace and becomes its owner.</summary>
public sealed record CreateWorkspace(string? Name) : ICommand<WorkspaceDto>, IAuthenticatedRequest;

internal sealed class CreateWorkspaceValidator : IValidator<CreateWorkspace>
{
    public void Validate(CreateWorkspace request, ValidationContext context) =>
        WorkspaceRules.Name(context, nameof(request.Name), request.Name);
}

internal sealed class CreateWorkspaceHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<CreateWorkspace, WorkspaceDto>
{
    public Task<WorkspaceDto> HandleAsync(CreateWorkspace command, CancellationToken cancellationToken)
    {
        var workspace = Workspace.Create(command.Name!, currentUser.UserId, clock.GetUtcNow());
        db.Workspaces.Add(workspace);
        return Task.FromResult(WorkspaceDto.From(workspace, currentUser.UserId));
    }
}
