namespace Injaz.Application.Security;

/// <summary>The caller of the current request.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The caller's user id.</summary>
    /// <exception cref="Common.Exceptions.UnauthorizedException">The caller is anonymous.</exception>
    Guid UserId { get; }
}
