using Injaz.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Injaz.Api.IntegrationTests.Infrastructure.ApiFactory))]

namespace Injaz.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container (one per test run). The same Postgres major
/// version as docker-compose is used so SQL behavior (collations, jsonb, constraints) matches production.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AllowedOrigin = "https://app.injaz.test";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    /// <summary>
    /// Starts the database, then boots the host once (which applies migrations). Booting here, single-threaded,
    /// matters: test classes run in parallel and the first CreateClient calls would otherwise race to start
    /// several hosts that migrate the same database at the same time.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        _ = Server;
    }

    /// <summary>A separate host on the same (already migrated) database with extra settings.</summary>
    public WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:MigrateOnStartup", "false");
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Auth:RefreshCookie:Secure", "false"); // TestServer speaks plain http
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        builder.UseSetting("RateLimiting:CredentialsPermitLimit", "10000");

        builder.ConfigureTestServices(services =>
        {
            // Test-only endpoints and use cases that exercise the shared pipeline (see TestEndpoints.cs).
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
            services.AddRequestHandlersFrom(typeof(ApiFactory).Assembly);
        });
    }
}
