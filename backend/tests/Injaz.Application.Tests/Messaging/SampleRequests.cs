using Injaz.Application.Messaging;
using Injaz.Application.Security;
using Injaz.Application.Validation;

namespace Injaz.Application.Tests.Messaging;

// ---- A public command with a validator ----

public sealed record CreateThing(string? Name, string? Email) : ICommand<string>, IPublicRequest;

internal sealed class CreateThingValidator : IValidator<CreateThing>
{
    public void Validate(CreateThing request, ValidationContext context)
    {
        context.RuleFor(nameof(request.Name), request.Name).NotEmpty().MaxLength(10);
        context.RuleFor(nameof(request.Email), request.Email).NotEmpty().EmailAddress();
    }
}

internal sealed class CreateThingHandler(CallLog log) : ICommandHandler<CreateThing, string>
{
    public Task<string> HandleAsync(CreateThing command, CancellationToken cancellationToken)
    {
        log.Calls.Add(nameof(CreateThingHandler));
        return Task.FromResult($"created:{command.Name}");
    }
}

// ---- A command whose handler fails ----

public sealed record FailingCommand : ICommand, IPublicRequest;

internal sealed class FailingCommandHandler : ICommandHandler<FailingCommand, Unit>
{
    public Task<Unit> HandleAsync(FailingCommand command, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");
}

// ---- A query that needs a signed-in user ----

public sealed record WhoAmI : IQuery<Guid>, IAuthenticatedRequest;

internal sealed class WhoAmIHandler(ICurrentUser currentUser) : IQueryHandler<WhoAmI, Guid>
{
    public Task<Guid> HandleAsync(WhoAmI query, CancellationToken cancellationToken) =>
        Task.FromResult(currentUser.UserId);
}

// ---- A request that forgot to declare an access policy ----

public sealed record NoPolicyQuery : IQuery<int>;

internal sealed class NoPolicyQueryHandler(CallLog log) : IQueryHandler<NoPolicyQuery, int>
{
    public Task<int> HandleAsync(NoPolicyQuery query, CancellationToken cancellationToken)
    {
        log.Calls.Add(nameof(NoPolicyQueryHandler));
        return Task.FromResult(42);
    }
}
