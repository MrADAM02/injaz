using System.Collections.Concurrent;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Injaz.Application.Messaging;

/// <summary>
/// Hand-rolled CQRS dispatcher. The request's runtime type is bridged to a strongly typed pipeline once
/// (cached per request type), so each call is a dictionary lookup plus ordinary generic DI resolution — no
/// per-call reflection.
/// </summary>
public sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<(Type Pipeline, Type Request, Type Result), object> Pipelines = new();

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return GetPipeline<TResult>(typeof(CommandPipeline<,>), command.GetType())
            .RunAsync(command, services, cancellationToken);
    }

    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return GetPipeline<TResult>(typeof(QueryPipeline<,>), query.GetType())
            .RunAsync(query, services, cancellationToken);
    }

    private static RequestPipeline<TResult> GetPipeline<TResult>(Type pipelineDefinition, Type requestType) =>
        (RequestPipeline<TResult>)Pipelines.GetOrAdd(
            (pipelineDefinition, requestType, typeof(TResult)),
            static key => Activator.CreateInstance(key.Pipeline.MakeGenericType(key.Request, key.Result))!);
}

internal abstract class RequestPipeline<TResult>
{
    public abstract Task<TResult> RunAsync(object request, IServiceProvider services, CancellationToken cancellationToken);

    protected static async Task AuthorizeAndValidateAsync<TRequest>(
        TRequest request, IServiceProvider services, CancellationToken cancellationToken)
        where TRequest : notnull
    {
        // Authorization runs first so anonymous/unauthorized callers learn nothing from validation errors.
        await services.GetRequiredService<IRequestAuthorizer>().AuthorizeAsync(request, cancellationToken);
        RequestValidation.ThrowIfInvalid(request, services.GetServices<IValidator<TRequest>>());
    }
}

internal sealed class CommandPipeline<TCommand, TResult> : RequestPipeline<TResult>
    where TCommand : ICommand<TResult>
{
    public override async Task<TResult> RunAsync(object request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var command = (TCommand)request;
        await AuthorizeAndValidateAsync(command, services, cancellationToken);

        var handler = services.GetRequiredService<ICommandHandler<TCommand, TResult>>();
        var result = await handler.HandleAsync(command, cancellationToken);

        // Single commit point: handlers never call SaveChanges themselves, so each command is one transaction.
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        return result;
    }
}

internal sealed class QueryPipeline<TQuery, TResult> : RequestPipeline<TResult>
    where TQuery : IQuery<TResult>
{
    public override async Task<TResult> RunAsync(object request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var query = (TQuery)request;
        await AuthorizeAndValidateAsync(query, services, cancellationToken);

        var handler = services.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        return await handler.HandleAsync(query, cancellationToken);
    }
}
