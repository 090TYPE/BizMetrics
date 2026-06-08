using System.Threading.Channels;

namespace BizMetrics.Infrastructure.Email;

/// <summary>
/// Hands an email off for asynchronous delivery so the request doesn't block on
/// SMTP. Backed by an in-process channel and drained by <see cref="EmailBackgroundService"/>.
/// In production this seam would point at a durable queue (e.g. SQS/Rabbit).
/// </summary>
public interface IEmailQueue
{
    ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default);
}

public class ChannelEmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct);
}
