using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.Controllers;

/// <summary>
/// Base for every API controller. [ApiController] must sit on the type (not the assembly): MVC's ProblemDetails
/// writer looks for it in endpoint metadata, and without it 401/403/429 responses from middleware get no body.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase;
