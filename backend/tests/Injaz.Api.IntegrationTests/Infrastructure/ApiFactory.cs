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
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async ValueTask InitializeAsync() => await _database.StartAsync();

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

        builder.ConfigureTestServices(services =>
        {
            // Test-only endpoints and use cases that exercise the shared pipeline (see TestEndpoints.cs).
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
            services.AddRequestHandlersFrom(typeof(ApiFactory).Assembly);
        });
    }
}
