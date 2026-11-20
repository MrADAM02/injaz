using Injaz.Api.Common;
using Injaz.Api.Realtime;
using Injaz.Application;
using Injaz.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

if (builder.Environment.IsProduction())
{
    // Fail at startup, not on the first invitation or cross-origin request.
    foreach (var required in new[] { "Cors:AllowedOrigins:0", "Invitations:FrontendBaseUrl" })
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[required]))
        {
            throw new InvalidOperationException($"Configuration '{required}' is required in Production.");
        }
    }
}

var app = builder.Build();

// First, so everything after it (rate limiting, logging) sees the real client IP and scheme.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHub<BoardHub>(BoardHub.Path, options => options.CloseOnAuthenticationExpiration = true);

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

await app.RunAsync();
