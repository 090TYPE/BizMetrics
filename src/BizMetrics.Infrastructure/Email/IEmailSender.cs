using Microsoft.Extensions.Logging;

namespace BizMetrics.Infrastructure.Email;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// Dev sender: writes the email to the log instead of contacting an SMTP server.
/// Swap for a real implementation (SendGrid/SES/SMTP) without touching callers.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _log;

    public LoggingEmailSender(ILogger<LoggingEmailSender> log) => _log = log;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _log.LogInformation(
            "EMAIL → {To}\n  Subject: {Subject}\n  {Body}",
            message.To, message.Subject, message.Body);
        return Task.CompletedTask;
    }
}
