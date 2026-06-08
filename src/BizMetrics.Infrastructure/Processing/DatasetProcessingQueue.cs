using System.Threading.Channels;

namespace BizMetrics.Infrastructure.Processing;

/// <summary>Queues a dataset id for asynchronous CSV processing off the request path.</summary>
public interface IDatasetProcessingQueue
{
    ValueTask EnqueueAsync(Guid datasetId, CancellationToken ct = default);
}

public class ChannelDatasetProcessingQueue : IDatasetProcessingQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Guid datasetId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(datasetId, ct);
}
