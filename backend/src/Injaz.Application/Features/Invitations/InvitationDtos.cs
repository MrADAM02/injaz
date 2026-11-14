using Injaz.Domain.Workspaces;

namespace Injaz.Application.Features.Invitations;

/// <summary>Bound from the <c>Invitations</c> configuration section.</summary>
public sealed class InvitationOptions
{
    public const string SectionName = "Invitations";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Public URL of the frontend; accept links are <c>{FrontendBaseUrl}/invite/{token}</c>.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    internal string AcceptUrl(string token) => $"{FrontendBaseUrl.TrimEnd('/')}/invite/{token}";
}

public sealed record InvitationDto(
    Guid Id,
    string Email,
    WorkspaceRole Role,
    Guid InvitedById,
    string InvitedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>Returned once, at creation: the only time the secret token (and therefore the link) is available.</summary>
public sealed record CreatedInvitationDto(InvitationDto Invitation, string Token, string AcceptUrl)
{
    public override string ToString() => $"CreatedInvitationDto {{ Id = {Invitation.Id} }}";
}

/// <summary>What the invite page shows before the user signs in or accepts.</summary>
public sealed record InvitationPreviewDto(
    Guid WorkspaceId,
    string WorkspaceName,
    string InvitedByName,
    string Email,
    WorkspaceRole Role,
    DateTimeOffset ExpiresAt,
    InvitationStatus Status);
