namespace Injaz.Application.Messaging;

/// <summary>
/// Entry point for executing use cases. Every request runs through the same pipeline:
/// authorize → validate → handle → (commands only) commit.
/// </summary>
public interface IDispatcher
{
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
