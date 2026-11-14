using Injaz.Domain.Common;

namespace Injaz.Domain.Users;

public sealed class User : Entity
{
    public const int EmailMaxLength = 254;
    public const int DisplayNameMaxLength = 100;
    public const int AvatarUrlMaxLength = 2048;

    private User()
    {
        // EF Core
    }

    /// <summary>Always stored normalized (trimmed, lower-case) so uniqueness is case-insensitive.</summary>
    public string Email { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public string? AvatarUrl { get; private set; }

    public Locale Locale { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Register(string email, string displayName, string passwordHash, Locale locale, DateTimeOffset now) =>
        new()
        {
            Email = NormalizeEmail(email),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            Locale = locale,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public static string NormalizeEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().ToLowerInvariant();
    }

    public void Rename(string displayName, DateTimeOffset now)
    {
        DisplayName = displayName.Trim();
        UpdatedAt = now;
    }

    /// <param name="avatarUrl">Null or whitespace removes the avatar.</param>
    public void ChangeAvatar(string? avatarUrl, DateTimeOffset now)
    {
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        UpdatedAt = now;
    }

    public void ChangeLocale(Locale locale, DateTimeOffset now)
    {
        Locale = locale;
        UpdatedAt = now;
    }

    public void ChangePasswordHash(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        UpdatedAt = now;
    }
}
