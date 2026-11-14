using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Injaz.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Injaz.Api.IntegrationTests.Infrastructure;

public sealed record Greet(string? Name) : ICommand<GreetResult>, IPublicRequest;

public sealed record GreetResult(string Greeting);

internal sealed class GreetValidator : IValidator<Greet>
{
    public void Validate(Greet request, ValidationContext context) =>
        context.RuleFor(nameof(request.Name), request.Name).NotEmpty().MaxLength(20);
}

internal sealed class GreetHandler : ICommandHandler<Greet, GreetResult>
{
    public Task<GreetResult> HandleAsync(Greet command, CancellationToken cancellationToken) =>
        Task.FromResult(new GreetResult($"Hello, {command.Name}"));
}

public sealed record Secret : IQuery<string>, IAuthenticatedRequest;

internal sealed class SecretHandler : IQueryHandler<Secret, string>
{
    public Task<string> HandleAsync(Secret query, CancellationToken cancellationToken) => Task.FromResult("classified");
}

/// <summary>
/// Exists only in the test host. Mirrors how real controllers stay thin: bind, dispatch, return.
/// [AllowAnonymous] bypasses the HTTP-level fallback policy so these tests exercise the Application-level checks.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("test")]
public sealed class TestController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost("greet")]
    public Task<GreetResult> GreetAsync(Greet command, CancellationToken cancellationToken) =>
        dispatcher.SendAsync(command, cancellationToken);

    [HttpGet("secret")]
    public Task<string> SecretAsync(CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(new Secret(), cancellationToken);

    [HttpGet("throw/{kind}")]
    public IActionResult Throw(string kind) => throw kind switch
    {
        "notFound" => new NotFoundException("widget"),
        "forbidden" => new ForbiddenException(),
        "conflict" => new ConflictException("widget.duplicate", "Widget exists."),
        "domain" => new DomainException("widget.locked", "Widget is locked."),
        _ => new InvalidOperationException("secret internal detail"),
    };
}
