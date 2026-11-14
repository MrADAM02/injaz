using Injaz.Application.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Injaz.Api.Common;

internal static class ErrorCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string InvalidRequest = "request.invalid";
    public const string ServerError = "server.error";

    /// <summary>Codes for responses produced without an exception (status-code pages, framework 401/403/404…).</summary>
    public static string ForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => InvalidRequest,
        StatusCodes.Status401Unauthorized => "auth.unauthorized",
        StatusCodes.Status403Forbidden => "auth.forbidden",
        StatusCodes.Status404NotFound => "notFound",
        StatusCodes.Status405MethodNotAllowed => "request.methodNotAllowed",
        StatusCodes.Status415UnsupportedMediaType => "request.unsupportedMediaType",
        StatusCodes.Status429TooManyRequests => "request.rateLimited",
        >= StatusCodes.Status500InternalServerError => ServerError,
        _ => $"http.{status}",
    };
}

internal static class ProblemErrors
{
    /// <summary>Shapes validation errors as <c>{ "title": [{ "code": "maxLength", "params": { "max": 100 } }] }</c>.</summary>
    public static Dictionary<string, object[]> From(IReadOnlyDictionary<string, IReadOnlyList<ValidationError>> errors) =>
        errors.ToDictionary(
            e => e.Key,
            e => e.Value.Select(ToJson).ToArray(),
            StringComparer.Ordinal);

    private static object ToJson(ValidationError error)
    {
        var json = new Dictionary<string, object>(StringComparer.Ordinal) { ["code"] = error.Code };
        if (error.Parameters is { Count: > 0 })
        {
            json["params"] = error.Parameters;
        }

        return json;
    }

    /// <summary>
    /// Model-binding failures (malformed JSON, wrong types) use the same shape as application validation errors.
    /// All semantic validation happens in the Application layer; this only covers "could not read the request".
    /// </summary>
    public static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => FieldName.Normalize(entry.Key),
                entry => new object[] { new Dictionary<string, object> { ["code"] = ValidationCodes.InvalidValue } },
                StringComparer.Ordinal);

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateProblemDetails(
            context.HttpContext,
            StatusCodes.Status400BadRequest,
            detail: "The request body or parameters could not be read.",
            instance: context.HttpContext.Request.Path);
        problem.Extensions["code"] = ErrorCodes.ValidationFailed;
        problem.Extensions["errors"] = errors;

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}
