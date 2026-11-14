using Injaz.Infrastructure;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Injaz.Application.Tests.Infrastructure.DatabaseFixture))]

namespace Injaz.Application.Tests.Infrastructure;

/// <summary>One migrated PostgreSQL container for the whole test assembly. Tests isolate themselves with unique data.</summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var app = CreateApp();
        await app.Services.MigrateDatabaseAsync();
    }

    public TestApp CreateApp() => new(ConnectionString);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
