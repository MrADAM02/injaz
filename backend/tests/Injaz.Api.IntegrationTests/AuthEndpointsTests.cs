using System.Net;
using System.Net.Http.Json;
using Injaz.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Injaz.Api.IntegrationTests;

public sealed class AuthEndpointsTests(ApiFactory factory)
{
    // Cookies are managed by hand so tests can replay old ones on purpose.
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_returns_access_token_and_sets_a_hardened_refresh_cookie()
    {
        var email = HttpTestExtensions.UniqueEmail();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = HttpTestExtensions.Password, displayName = "Sara", locale = "en" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/me", response.Headers.Location?.OriginalString);

        var body = await response.JsonAsync();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal(email, body.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal("en", body.GetProperty("user").GetProperty("locale").GetString());
        Assert.False(body.TryGetProperty("refreshToken", out _), "The refresh token must never appear in the body.");

        var cookie = response.RefreshSetCookie().ToLowerInvariant();
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/api/auth", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("domain=", cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_with_invalid_body_returns_field_errors()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email = "nope", password = "123" }, Ct);

        var errors = (await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed")).GetProperty("errors");
        Assert.Equal("email", errors.GetProperty("email")[0].GetProperty("code").GetString());
        Assert.Equal("minLength", errors.GetProperty("password")[0].GetProperty("code").GetString());
        Assert.Equal("required", errors.GetProperty("displayName")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Access_token_authenticates_api_me()
    {
        var (accessToken, _, registered) = await _client.RegisterAsync();

        var response = await _client.SendWithBearerAsync(HttpMethod.Get, "/api/me", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.JsonAsync();
        Assert.Equal(registered.GetProperty("user").GetProperty("id").GetString(), me.GetProperty("id").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not.a.jwt")]
    public async Task Missing_or_invalid_access_token_returns_problem_401(string? accessToken)
    {
        var response = await _client.SendWithBearerAsync(HttpMethod.Get, "/api/me", accessToken);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_invalid_credentials()
    {
        var (_, _, registered) = await _client.RegisterAsync();
        var email = registered.GetProperty("user").GetProperty("email").GetString();

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" }, Ct);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalidCredentials");
    }

    [Fact]
    public async Task Refresh_requires_the_csrf_header()
    {
        var (_, refreshCookie, _) = await _client.RegisterAsync();

        var response = await _client.PostWithRefreshCookieAsync("/api/auth/refresh", refreshCookie, csrfHeader: false);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, "auth.csrfHeaderMissing");
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_flags_an_immediate_replay_as_superseded()
    {
        var (_, firstCookie, _) = await _client.RegisterAsync();

        var refreshed = await _client.PostWithRefreshCookieAsync("/api/auth/refresh", firstCookie);
        var replay = await _client.PostWithRefreshCookieAsync("/api/auth/refresh", firstCookie);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.NotEqual(firstCookie, refreshed.RefreshCookieValue());
        Assert.False(string.IsNullOrEmpty((await refreshed.JsonAsync()).GetProperty("accessToken").GetString()));
        await replay.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.refreshSuperseded");
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_401()
    {
        var response = await _client.PostWithRefreshCookieAsync("/api/auth/refresh", cookieValue: null);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalidRefreshToken");
    }

    [Fact]
    public async Task Logout_clears_the_cookie_and_ends_the_session()
    {
        var (_, refreshCookie, _) = await _client.RegisterAsync();

        var logout = await _client.PostWithRefreshCookieAsync("/api/auth/logout", refreshCookie);
        var refresh = await _client.PostWithRefreshCookieAsync("/api/auth/refresh", refreshCookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970", logout.RefreshSetCookie().ToLowerInvariant(), StringComparison.Ordinal);
        await refresh.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalidRefreshToken");
    }

    [Fact]
    public async Task Profile_can_be_patched_and_enums_travel_as_camelCase_strings()
    {
        var (accessToken, _, _) = await _client.RegisterAsync();

        var ok = await _client.SendWithBearerAsync(HttpMethod.Patch, "/api/me", accessToken, new { displayName = "أحمد", locale = "en" });
        var bad = await _client.SendWithBearerAsync(HttpMethod.Patch, "/api/me", accessToken, new { locale = "fr" });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var me = await ok.JsonAsync();
        Assert.Equal("أحمد", me.GetProperty("displayName").GetString());
        Assert.Equal("en", me.GetProperty("locale").GetString());

        var problem = await bad.AssertProblemAsync(HttpStatusCode.BadRequest, "validation.failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("locale", out _));
    }

    [Fact]
    public async Task Change_password_returns_a_new_session_cookie()
    {
        var (accessToken, oldCookie, _) = await _client.RegisterAsync();

        var response = await _client.SendWithBearerAsync(
            HttpMethod.Put, "/api/me/password", accessToken,
            new { currentPassword = HttpTestExtensions.Password, newPassword = "a-brand-new-password" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(oldCookie, response.RefreshCookieValue());
        var oldSession = await _client.PostWithRefreshCookieAsync("/api/auth/refresh", oldCookie);
        await oldSession.AssertProblemAsync(HttpStatusCode.Unauthorized, "auth.invalidRefreshToken");
    }

    [Theory]
    [InlineData(ApiFactory.AllowedOrigin, true)]
    [InlineData("https://evil.example", false)]
    public async Task Cors_allows_credentials_only_for_the_configured_frontend(string origin, bool allowed)
    {
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/auth/refresh");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "x-requested-with");

        var response = await _client.SendAsync(preflight, Ct);

        Assert.Equal(allowed, response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins) && origins.Single() == origin);
        if (allowed)
        {
            Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        }
    }

    [Fact]
    public async Task Credential_endpoints_are_rate_limited()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:CredentialsPermitLimit", "2"));
        var client = limited.CreateClient();
        var body = new { email = HttpTestExtensions.UniqueEmail(), password = "whatever-password" };

        for (var i = 0; i < 2; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", body, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var limitedResponse = await client.PostAsJsonAsync("/api/auth/login", body, Ct);
        await limitedResponse.AssertProblemAsync(HttpStatusCode.TooManyRequests, "request.rateLimited");
    }
}
