using Microsoft.Extensions.Options;

namespace Injaz.Api.Auth;

/// <summary>Bound from <c>Auth:RefreshCookie</c>.</summary>
public sealed class RefreshCookieOptions
{
    public const string SectionName = "Auth:RefreshCookie";

    public string Name { get; set; } = "injaz_rt";

    /// <summary>Only the auth endpoints ever receive the cookie.</summary>
    public string Path { get; set; } = "/api/auth";

    /// <summary>Must be true in production. Development turns it off for plain-http localhost (Safari ignores Secure there).</summary>
    public bool Secure { get; set; } = true;
}

/// <summary>
/// The refresh token lives only in this cookie: httpOnly (unreachable from JavaScript), SameSite=Strict, scoped to
/// <c>/api/auth</c>, and host-only (no Domain attribute) — the frontend never needs to read it. Because the app
/// (<c>app.example.com</c>) and the API (<c>api.example.com</c>) are same-site, Strict still sends it on fetch.
/// </summary>
public sealed class RefreshTokenCookie(IOptions<RefreshCookieOptions> options)
{
    private RefreshCookieOptions Settings => options.Value;

    public string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Cookies.TryGetValue(Settings.Name, out var value) ? value : null;
    }

    public void Write(HttpResponse response, string refreshToken, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        var cookie = CreateOptions();
        cookie.Expires = expiresAt;
        response.Cookies.Append(Settings.Name, refreshToken, cookie);
    }

    public void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(Settings.Name, CreateOptions());
    }

    private CookieOptions CreateOptions() => new()
    {
        HttpOnly = true,
        Secure = Settings.Secure,
        SameSite = SameSiteMode.Strict,
        Path = Settings.Path,
        IsEssential = true,
    };
}
