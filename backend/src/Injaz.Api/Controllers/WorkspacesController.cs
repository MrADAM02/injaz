using Injaz.Application.Features.Workspaces;
using Injaz.Application.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.Controllers;

[Route("api/workspaces")]
public sealed class WorkspacesController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<WorkspaceDto>> ListAsync(CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new ListMyWorkspaces(), cancellationToken);

    [HttpPost]
    [ProducesResponseType<WorkspaceDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(CreateWorkspace command, CancellationToken cancellationToken)
    {
        var workspace = await dispatcher.SendAsync(command, cancellationToken);
        return Created($"/api/workspaces/{workspace.Id}", workspace);
    }

    [HttpGet("{workspaceId:guid}")]
    public Task<WorkspaceDto> GetAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new GetWorkspace(workspaceId), cancellationToken);

    [HttpPatch("{workspaceId:guid}")]
    public Task<WorkspaceDto> RenameAsync(Guid workspaceId, RenameWorkspaceBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new RenameWorkspace(workspaceId, body.Name), cancellationToken);

    [HttpDelete("{workspaceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new DeleteWorkspace(workspaceId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{workspaceId:guid}/transfer-ownership")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> TransferOwnershipAsync(Guid workspaceId, TransferOwnershipBody body, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new TransferOwnership(workspaceId, body.UserId), cancellationToken);
        return NoContent();
    }
}

public sealed record RenameWorkspaceBody(string? Name);

public sealed record TransferOwnershipBody(Guid UserId);
