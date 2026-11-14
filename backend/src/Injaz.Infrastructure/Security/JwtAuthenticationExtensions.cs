using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Injaz.Infrastructure.Security;

public static class JwtAuthenticationExtensions
{
    /// <summary>Validates the access tokens issued by <see cref="JwtTokenService"/> using the same <see cref="JwtOptions"/>.</summary>
    /// <param name="queryTokenPathPrefix">
    /// Paths (the SignalR hubs) where the token may also arrive as <c>?access_token=</c>: browsers can't set headers on
    /// WebSocket connections. Everywhere else only the Authorization header is accepted. Keep the reverse proxy from
    /// logging query strings on this path.
    /// </param>
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services, string queryTokenPathPrefix = "/hubs")
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwtOptions, clock) =>
            {
                var jwt = jwtOptions.Value;

                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"].ToString();
                        if (token.Length > 0 && context.Request.Path.StartsWithSegments(queryTokenPathPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };

                bearer.MapInboundClaims = false; // keep "sub" as "sub"
                bearer.TimeProvider = clock;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        return services;
    }
}
