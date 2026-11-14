using System.Data.Common;
using Injaz.Application.Persistence;
using Injaz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Injaz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = BuildConnectionString(configuration);

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton(TimeProvider.System);

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");

        return services;
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Default' is not configured.");

        // The chiseled runtime image ships without Kerberos libraries; unless explicitly configured, skip the
        // GSS encryption probe Npgsql would otherwise attempt (logging a native-library error on first connect).
        // A plain DbConnectionStringBuilder is used for the check because Npgsql's builder reports every known
        // keyword as present.
        var explicitKeys = new DbConnectionStringBuilder { ConnectionString = raw };
        if (explicitKeys.ContainsKey("GSS Encryption Mode") || explicitKeys.ContainsKey("GssEncryptionMode"))
        {
            return raw;
        }

        return new NpgsqlConnectionStringBuilder(raw) { GssEncryptionMode = GssEncryptionMode.Disable }.ConnectionString;
    }

    /// <summary>Applies pending EF Core migrations. Enabled via <c>Database:MigrateOnStartup</c> (Development only by default).</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
