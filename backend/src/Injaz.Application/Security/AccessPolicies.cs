namespace Injaz.Application.Security;

// Every request must declare exactly one access policy by implementing one of these markers.
// Requests that declare none are rejected at runtime (fail closed) and by an architecture test.
// Phase 3 adds IAuthorizedRequest for workspace-scoped, role-checked requests.

/// <summary>The request may be executed anonymously (e.g. register, login).</summary>
public interface IPublicRequest
{
}

/// <summary>The request requires a signed-in user but no workspace role (e.g. get my profile, create a workspace).</summary>
public interface IAuthenticatedRequest
{
}
