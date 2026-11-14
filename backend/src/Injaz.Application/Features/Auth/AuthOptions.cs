namespace Injaz.Application.Features.Auth;

/// <summary>Bound from the <c>Auth</c> configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>How long a refresh token (and therefore an idle session) lives. Each refresh slides it forward.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// A just-rotated token presented again within this window is treated as a benign race (two tabs refreshing
    /// at once) rather than theft: the request fails with <c>auth.refreshSuperseded</c> but the session survives.
    /// </summary>
    public TimeSpan RefreshReuseGracePeriod { get; set; } = TimeSpan.FromSeconds(30);
}
