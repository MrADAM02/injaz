using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Injaz.Api.Common;

public static class RateLimitPolicies
{
    /// <summary>Endpoints that accept a password: register, login, change password.</summary>
    public const string Credentials = "credentials";
}

/// <summary>Bound from <c>RateLimiting</c>.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int CredentialsPermitLimit { get; set; } = 10;

    public TimeSpan CredentialsWindow { get; set; } = TimeSpan.FromMinutes(1);
}

internal static class RateLimitingSetup
{
    /// <remarks>
    /// Partitioned by client IP. Behind the reverse proxy the IP comes from X-Forwarded-For, which is only honoured
    /// from <c>ReverseProxy:TrustedNetworks</c> (see <see cref="ReverseProxySetup"/>).
    /// </remarks>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicies.Credentials, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.CredentialsPermitLimit,
                        Window = settings.CredentialsWindow,
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Too many attempts. Please wait a moment and try again.",
                    },
                });
            };
        });

        return services;
    }
}
