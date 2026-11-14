using Injaz.Application.Features.Invitations;
using Injaz.Application.Features.Workspaces;
using Injaz.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.Controllers;

/// <summary>The invitee's side of an invitation, addressed by the secret token from the link.</summary>
[Route("api/invitations/{token}")]
public sealed class InvitationsController(IDispatcher dispatcher) : ApiControllerBase
{
    /// <summary>Preview for the invite page — works before signing in.</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<InvitationPreviewDto> PreviewAsync(string token, CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new GetInvitation(token), cancellationToken);

    [HttpPost("accept")]
    public Task<WorkspaceDto> AcceptAsync(string token, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(new AcceptInvitation(token), cancellationToken);
}
