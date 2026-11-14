using Injaz.Application.Common.Exceptions;

namespace Injaz.Application.Features.Auth;

internal static class AuthErrors
{
    public static ConflictException EmailTaken() =>
        new("auth.emailTaken", "An account with this email already exists.");

    /// <summary>Same error for unknown email and wrong password, so accounts can't be enumerated via login.</summary>
    public static UnauthorizedException InvalidCredentials() =>
        new("auth.invalidCredentials", "The email or password is incorrect.");

    public static UnauthorizedException InvalidRefreshToken() =>
        new("auth.invalidRefreshToken", "The session has expired. Please sign in again.");

    public static UnauthorizedException RefreshSuperseded() =>
        new("auth.refreshSuperseded", "This session was just refreshed by another request. Retry with the latest cookie.");
}

/// <summary>Password policy shared by registration and password change.</summary>
internal static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>Upper bound keeps hashing cost bounded (PBKDF2 cost grows with input length).</summary>
    public const int MaxLength = 128;
}
