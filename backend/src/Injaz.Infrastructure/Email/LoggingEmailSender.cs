using Injaz.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Injaz.Infrastructure.Email;

/// <summary>
/// MVP stand-in for a real email provider. Logs recipient and subject at Information; the body (which contains
/// secret links) only at Debug, so production logs don't collect working invitation tokens.
/// </summary>
internal sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogEmail(logger, message.To, message.Subject);
        LogBody(logger, message.TextBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email (not sent — no provider configured) to {To}: {Subject}")]
    private static partial void LogEmail(ILogger logger, string to, string subject);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Email body:\n{Body}")]
    private static partial void LogBody(ILogger logger, string body);
}
