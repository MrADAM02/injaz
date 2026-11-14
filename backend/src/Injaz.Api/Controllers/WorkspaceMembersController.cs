using Injaz.Application.Features.Invitations;
using Injaz.Application.Features.Members;
using Injaz.Application.Messaging;
using Injaz.Domain.Workspaces;
using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.Controllers;

[Route("api/workspaces/{workspaceId:guid}/members")]
public sealed class WorkspaceMembersController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<MemberDto>> ListAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new ListMembers(workspaceId), cancellationToken);

    [HttpPatch("{userId:guid}")]
    public Task<MemberDto> ChangeRoleAsync(Guid workspaceId, Guid userId, ChangeMemberRoleBody body, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new ChangeMemberRole(workspaceId, userId, body.Role), cancellationToken);

    /// <summary>Removes a member, or — when <paramref name="userId"/> is the caller — leaves the workspace.</summary>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new RemoveMember(workspaceId, userId), cancellationToken);
        return NoContent();
    }
}

[Route("api/workspaces/{workspaceId:guid}/invitations")]
public sealed class WorkspaceInvitationsController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<InvitationDto>> ListAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new ListInvitations(workspaceId), cancellationToken);

    [HttpPost]
    [ProducesResponseType<CreatedInvitationDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(Guid workspaceId, CreateInvitationBody body, CancellationToken cancellationToken)
    {
        var created = await dispatcher.SendAsync(new CreateInvitation(workspaceId, body.Email, body.Role), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpDelete("{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeAsync(Guid workspaceId, Guid invitationId, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(new RevokeInvitation(workspaceId, invitationId), cancellationToken);
        return NoContent();
    }
}

public sealed record ChangeMemberRoleBody(WorkspaceRole? Role);

public sealed record CreateInvitationBody(string? Email, WorkspaceRole? Role);
