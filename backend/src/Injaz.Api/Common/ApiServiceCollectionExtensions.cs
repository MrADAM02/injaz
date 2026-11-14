using System.Text.Json;
using System.Text.Json.Serialization;
using Injaz.Api.Auth;
using Injaz.Application.Features.Auth;
using Injaz.Application.Features.Invitations;
using Injaz.Application.Security;
using Injaz.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.Common;

internal static class ApiServiceCollectionExtensions
{
    public const string ConnectionIdHeader = "X-Connection-Id";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddControllers(options =>
            {
                // Validation lives in the Application layer; don't let MVC invent [Required] from nullability.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));

        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = ProblemErrors.InvalidModelState);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
            context.ProblemDetails.Extensions.TryAdd("code", ErrorCodes.ForStatus(status));
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        });
        services.AddExceptionHandler<AppExceptionHandler>();

        services.AddApiSecurity(configuration);
        services.AddApiRateLimiting(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddOpenApi();

        return services;
    }

    private static void AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName));
        services.AddOptions<InvitationOptions>().Bind(configuration.GetSection(InvitationOptions.SectionName));
        services.AddOptions<RefreshCookieOptions>().Bind(configuration.GetSection(RefreshCookieOptions.SectionName));
        services.AddSingleton<RefreshTokenCookie>();

        services.AddJwtBearerAuthentication();

        // Defense in depth: every endpoint requires a signed-in user unless it opts out with [AllowAnonymous].
        // The Application layer's access policies remain the source of truth.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .AllowCredentials()
            .WithHeaders("Content-Type", "Authorization", RequireCsrfHeaderAttribute.HeaderName, ConnectionIdHeader)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .SetPreflightMaxAge(TimeSpan.FromHours(1))));
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new OptionalJsonConverterFactory());
    }
}
