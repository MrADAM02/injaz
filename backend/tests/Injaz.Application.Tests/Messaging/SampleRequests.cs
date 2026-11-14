using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
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

// ---- A workspace-scoped command that needs Admin and reports what the handler saw ----

public sealed record AdminOnly(Guid WorkspaceId) : ICommand<string>, IAuthorizedRequest
{
    public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

    public Permission Permission => Permission.ManageMembers;
}

internal sealed class AdminOnlyHandler(ICurrentAccess access) : ICommandHandler<AdminOnly, string>
{
    public Task<string> HandleAsync(AdminOnly command, CancellationToken cancellationToken) =>
        Task.FromResult(access.Access.Role.ToString());
}

// ---- A command with a post-commit side effect ----

public sealed record WithSideEffect(bool Fail) : ICommand, IPublicRequest;

internal sealed class WithSideEffectHandler(IAfterCommit afterCommit, CallLog log) : ICommandHandler<WithSideEffect, Unit>
{
    public Task<Unit> HandleAsync(WithSideEffect command, CancellationToken cancellationToken)
    {
        afterCommit.Enqueue(_ =>
        {
            log.Calls.Add("side-effect");
            return Task.CompletedTask;
        });
        afterCommit.Enqueue(_ => throw new InvalidOperationException("a failing side effect must not fail the request"));

        return command.Fail ? throw new InvalidOperationException("handler failed") : Task.FromResult(Unit.Value);
    }
}
