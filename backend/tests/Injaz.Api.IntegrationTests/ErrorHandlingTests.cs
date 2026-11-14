using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Injaz.Api.IntegrationTests.Infrastructure;

namespace Injaz.Api.IntegrationTests;

public sealed class ErrorHandlingTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_reports_healthy_when_database_is_reachable()
    {
        var response = await _client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Valid_command_round_trips_through_the_pipeline()
    {
        var response = await _client.PostAsJsonAsync("/test/greet", new { name = "Injaz" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Hello, Injaz", body.GetProperty("greeting").GetString());
    }

    [Fact]
    public async Task Validation_failure_returns_problem_with_field_codes_and_params()
    {
        var response = await _client.PostAsJsonAsync("/test/greet", new { name = new string('x', 25) }, Ct);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
        var error = problem.GetProperty("errors").GetProperty("name")[0];
        Assert.Equal("maxLength", error.GetProperty("code").GetString());
        Assert.Equal(20, error.GetProperty("params").GetProperty("max").GetInt32());
    }

    [Fact]
    public async Task Malformed_json_returns_the_same_validation_shape()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/test/greet", content, Ct);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
        Assert.Equal(JsonValueKind.Object, problem.GetProperty("errors").ValueKind);
    }

    [Fact]
    public async Task Anonymous_caller_gets_401_for_authenticated_request()
    {
        var response = await _client.GetAsync("/test/secret", Ct);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Theory]
    [InlineData("notFound", HttpStatusCode.NotFound, "widget.notFound")]
    [InlineData("forbidden", HttpStatusCode.Forbidden, "auth.forbidden")]
    [InlineData("conflict", HttpStatusCode.Conflict, "widget.duplicate")]
    [InlineData("domain", HttpStatusCode.UnprocessableEntity, "widget.locked")]
    public async Task Application_exceptions_map_to_status_and_code(string kind, HttpStatusCode status, string code)
    {
        var response = await _client.GetAsync($"/test/throw/{kind}", Ct);

        await AssertProblemAsync(response, status, code);
    }

    [Fact]
    public async Task Unexpected_exception_returns_500_without_leaking_details()
    {
        var response = await _client.GetAsync("/test/throw/crash", Ct);

        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "server.error");
        Assert.DoesNotContain("secret internal detail", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_route_returns_401_to_anonymous_callers_and_404_to_signed_in_ones()
    {
        // The fallback policy also covers unmatched routes, so anonymous callers learn nothing about the API surface.
        var anonymous = await _client.GetAsync("/api/does-not-exist", Ct);
        var (accessToken, _, _) = await _client.RegisterAsync();
        var signedIn = await _client.SendWithBearerAsync(HttpMethod.Get, "/api/does-not-exist", accessToken);

        await AssertProblemAsync(anonymous, HttpStatusCode.Unauthorized, "auth.unauthorized");
        await AssertProblemAsync(signedIn, HttpStatusCode.NotFound, "notFound");
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _), "ProblemDetails should carry a traceId.");
        return problem;
    }
}
