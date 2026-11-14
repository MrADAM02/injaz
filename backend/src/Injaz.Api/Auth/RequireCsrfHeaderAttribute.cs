using Injaz.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Injaz.Api.Auth;

/// <summary>
/// Defense in depth for cookie-authenticated endpoints (refresh, logout). Besides SameSite=Strict, the request must
/// carry a custom header. Plain HTML forms and simple cross-origin requests can't add one, and a cross-origin
/// fetch that does triggers a CORS preflight that only the configured frontend origin passes.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireCsrfHeaderAttribute : Attribute, IResourceFilter
{
    public const string HeaderName = "X-Requested-With";

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.HttpContext.Request.Headers[HeaderName]))
        {
            throw new ForbiddenException("auth.csrfHeaderMissing", $"The '{HeaderName}' header is required.");
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
