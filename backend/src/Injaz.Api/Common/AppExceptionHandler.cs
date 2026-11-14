using Injaz.Application.Common.Exceptions;
using Injaz.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Injaz.Api.Common;

/// <summary>
/// Single place where exceptions become HTTP responses. Every error body is RFC 9457 ProblemDetails with
/// two extensions: <c>code</c> (stable i18n key) and, for validation, <c>errors</c> ({ field: [{ code, params? }] }).
/// </summary>
internal sealed partial class AppExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // Client went away; nobody is listening for a body.
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var (status, code, detail) = exception switch
        {
            ValidationException e => (StatusCodes.Status400BadRequest, e.Code, e.Message),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, e.Code, e.Message),
            ForbiddenException e => (StatusCodes.Status403Forbidden, e.Code, e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Code, e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, e.Code, e.Message),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Code, e.Message),
            BadHttpRequestException e => (e.StatusCode, ErrorCodes.InvalidRequest, "The request could not be read."),
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.ServerError,
                environment.IsDevelopment() ? exception.Message : "An unexpected error occurred."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = code;

        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = ProblemErrors.From(validation.Errors);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);
}
