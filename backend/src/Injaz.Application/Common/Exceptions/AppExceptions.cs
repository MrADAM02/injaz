using Injaz.Application.Validation;

namespace Injaz.Application.Common.Exceptions;

/// <summary>
/// Base for expected failures that the API maps to ProblemDetails. <see cref="Code"/> is a stable key
/// (e.g. <c>card.notFound</c>) that the frontend translates; the message is for developers and logs.
/// </summary>
public abstract class AppException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

/// <summary>400 — the request is well-formed but its values break validation rules.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, IReadOnlyList<ValidationError>> errors)
    : AppException("validation.failed", "One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, IReadOnlyList<ValidationError>> Errors { get; } = errors;
}

/// <summary>401 — the caller is not signed in (or their token is invalid).</summary>
public sealed class UnauthorizedException(string code = "auth.unauthorized", string message = "Authentication is required.")
    : AppException(code, message);

/// <summary>403 — the caller is signed in but lacks the required role.</summary>
public sealed class ForbiddenException(string code = "auth.forbidden", string message = "You do not have permission to perform this action.")
    : AppException(code, message);

/// <summary>404 — <paramref name="resource"/> is a camelCase resource name, producing codes like <c>card.notFound</c>.</summary>
public sealed class NotFoundException(string resource)
    : AppException($"{resource}.notFound", $"The requested {resource} was not found.");

/// <summary>409 — the request conflicts with current state (duplicate, concurrent edit, …).</summary>
public sealed class ConflictException(string code, string message, Exception? innerException = null)
    : AppException(code, message, innerException);
