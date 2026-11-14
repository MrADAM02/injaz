using Injaz.Domain.Users;

namespace Injaz.Application.Features.Profile;

/// <summary>The signed-in user's own profile (includes email; other users will get a slimmer member DTO).</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    Locale Locale,
    DateTimeOffset CreatedAt)
{
    public static UserDto From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.Locale, user.CreatedAt);
    }
}
