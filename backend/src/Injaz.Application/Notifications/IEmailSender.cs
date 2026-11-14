namespace Injaz.Application.Notifications;

/// <summary>
/// Outgoing email. The MVP implementation only logs; a real provider (SMTP, Resend, Postmark…) plugs in here.
/// Always send from an <see cref="Persistence.IAfterCommit"/> action so nothing goes out for a rolled-back command.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string TextBody);
