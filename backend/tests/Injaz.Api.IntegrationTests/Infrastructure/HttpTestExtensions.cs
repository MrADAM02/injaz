using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Injaz.Api.IntegrationTests.Infrastructure;

internal static class HttpTestExtensions
{
    public const string RefreshCookieName = "injaz_rt";
    public const string Password = "correct-horse-battery";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>The raw Set-Cookie header for the refresh cookie.</summary>
    public static string RefreshSetCookie(this HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal));

    public static string RefreshCookieValue(this HttpResponseMessage response) =>
        response.RefreshSetCookie().Split(';')[0][(RefreshCookieName.Length + 1)..];

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    public static async Task<(string AccessToken, string RefreshCookie, JsonElement Body)> RegisterAsync(
        this HttpClient client, string? email = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = email ?? UniqueEmail(), password = Password, displayName = "Test User" },
            Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.JsonAsync();
        return (body.GetProperty("accessToken").GetString()!, response.RefreshCookieValue(), body);
    }

    /// <summary>POST to a cookie-authenticated endpoint the way the frontend does (cookie + CSRF header).</summary>
    public static async Task<HttpResponseMessage> PostWithRefreshCookieAsync(
        this HttpClient client, string path, string? cookieValue, bool csrfHeader = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (cookieValue is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshCookieName}={cookieValue}");
        }

        if (csrfHeader)
        {
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        }

        return await client.SendAsync(request, Ct);
    }

    public static async Task<HttpResponseMessage> SendWithBearerAsync(
        this HttpClient client, HttpMethod method, string path, string? accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, Ct);
    }

    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.JsonAsync();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }
}
