using System.Threading.Channels;

namespace Gateway.Models
{
    // Singleton service to hold the shared channel
    public class ProcessingChannel
    {
        // Simple unbounded channel for this quick version
        private readonly Channel<QueuedMessage> _channel = Channel.CreateUnbounded<QueuedMessage>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

        public ChannelReader<QueuedMessage> Reader => _channel.Reader;
        public ChannelWriter<QueuedMessage> Writer => _channel.Writer;
    }
}
