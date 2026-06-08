using BizMetrics.Infrastructure.Email;
using Xunit;

namespace BizMetrics.Tests;

public class EmailQueueTests
{
    [Fact]
    public async Task Enqueued_message_is_readable_from_the_channel()
    {
        var queue = new ChannelEmailQueue();
        var msg = new EmailMessage("a@b.com", "Hi", "Body");

        await queue.EnqueueAsync(msg);
        var read = await queue.Reader.ReadAsync();

        Assert.Equal(msg, read);
    }

    [Fact]
    public async Task Background_service_delivers_queued_messages_to_the_sender()
    {
        var queue = new ChannelEmailQueue();
        var sender = new RecordingSender();
        var svc = new EmailBackgroundService(queue, sender,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await svc.StartAsync(cts.Token);
        await queue.EnqueueAsync(new EmailMessage("x@y.com", "S", "B"), cts.Token);

        // Wait for the drain loop to pick it up.
        while (sender.Sent.Count == 0 && !cts.IsCancellationRequested)
            await Task.Delay(20, cts.Token);

        await svc.StopAsync(CancellationToken.None);
        Assert.Single(sender.Sent);
        Assert.Equal("x@y.com", sender.Sent[0].To);
    }

    private sealed class RecordingSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
