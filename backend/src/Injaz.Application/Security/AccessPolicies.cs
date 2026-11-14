namespace Injaz.Application.Security;

// Every request must declare exactly one access policy by implementing one of these interfaces.
// Requests that declare none are rejected at runtime (fail closed) and by an architecture test.

/// <summary>The request may be executed anonymously (e.g. register, login).</summary>
public interface IPublicRequest
{
}

/// <summary>The request requires a signed-in user but no workspace role (e.g. get my profile, create a workspace).</summary>
public interface IAuthenticatedRequest
{
}

/// <summary>
/// The request targets a resource inside a workspace and needs a minimum role there.
/// Non-members get <c>{resource}.notFound</c> (existence isn't revealed); members without the role get 403.
/// After authorization the caller's membership is available to the handler via <see cref="ICurrentAccess"/>.
/// </summary>
public interface IAuthorizedRequest
{
    ResourceScope Scope { get; }

    Permission Permission { get; }
}

/// <summary>The caller's role in the workspace the current request was authorized against.</summary>
public interface ICurrentAccess
{
    /// <exception cref="InvalidOperationException">The request isn't an <see cref="IAuthorizedRequest"/>.</exception>
    WorkspaceAccess Access { get; }
}
