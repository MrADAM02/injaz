namespace Injaz.Application.Messaging;

/// <summary>Marker for anything the <see cref="IDispatcher"/> can execute.</summary>
public interface IRequest<out TResult>
{
}

/// <summary>A state-changing use case. Changes are committed by the dispatcher after the handler succeeds.</summary>
public interface ICommand<out TResult> : IRequest<TResult>
{
}

/// <summary>A command with no meaningful result.</summary>
public interface ICommand : ICommand<Unit>
{
}

/// <summary>A read-only use case. The dispatcher never commits after a query.</summary>
public interface IQuery<out TResult> : IRequest<TResult>
{
}

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

/// <summary>The "void" result type for commands.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value;
}
