using Microsoft.Extensions.Logging;

namespace Injaz.Application.Persistence;

/// <summary>
/// Side effects that must only happen once the command's changes are durable (emails now, real-time events later).
/// The dispatcher runs them after a successful commit; if the command fails they never run.
/// </summary>
public interface IAfterCommit
{
    void Enqueue(Func<CancellationToken, Task> action);
}

internal sealed partial class AfterCommitQueue(ILogger<AfterCommitQueue> logger) : IAfterCommit
{
    private readonly List<Func<CancellationToken, Task>> _actions = [];

    public void Enqueue(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _actions.Add(action);
    }

    /// <summary>
    /// Failures are logged, not thrown: the command already succeeded, so the caller must not see an error.
    /// Cancellation is not propagated either — a client disconnecting shouldn't drop an email.
    /// </summary>
    public async Task FlushAsync()
    {
        foreach (var action in _actions)
        {
            try
            {
                await action(CancellationToken.None);
            }
#pragma warning disable CA1031 // Deliberately catch everything: post-commit work must not fail the request.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogFailure(logger, ex);
            }
        }

        _actions.Clear();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A post-commit action failed")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
