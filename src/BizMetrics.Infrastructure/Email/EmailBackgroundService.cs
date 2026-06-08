using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BizMetrics.Infrastructure.Email;

/// <summary>Drains the email queue and delivers each message via <see cref="IEmailSender"/>.</summary>
public class EmailBackgroundService : BackgroundService
{
    private readonly ChannelEmailQueue _queue;
    private readonly IEmailSender _sender;
    private readonly ILogger<EmailBackgroundService> _log;

    public EmailBackgroundService(
        ChannelEmailQueue queue,
        IEmailSender sender,
        ILogger<EmailBackgroundService> log)
    {
        _queue = queue;
        _sender = sender;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _sender.SendAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Don't let one failed delivery kill the drain loop.
                _log.LogError(ex, "Failed to send email to {To}", message.To);
            }
        }
    }
}
