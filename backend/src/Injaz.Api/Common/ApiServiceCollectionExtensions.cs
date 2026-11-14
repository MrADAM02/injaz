using System.Text.Json;
using System.Text.Json.Serialization;
using Injaz.Application.Security;
using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.Common;

internal static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services)
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

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddOpenApi();

        return services;
    }

    private static void ConfigureJson(JsonSerializerOptions options) =>
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
}
