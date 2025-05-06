using Microsoft.Extensions.Options;
using Nodes.Configs;
using System.Threading.Channels;

namespace Nodes.Models
{
    public class ProcessingChannel
    {
        public ChannelReader<QueuedMessage> Reader { get; }
        public ChannelWriter<QueuedMessage> Writer { get; }

        public ProcessingChannel(IOptions<NodeConfigs> nodeConfigs)
        {
            // Use a Bounded channel to prevent excessive memory use if processing falls behind
            var options = new BoundedChannelOptions(nodeConfigs.Value.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait, // Wait for space when writing if full
                SingleReader = false, // Allow multiple concurrent readers (processors)
                SingleWriter = true   // Typically only the MQTT handler writes
            };
            var channel = Channel.CreateBounded<QueuedMessage>(options);
            Reader = channel.Reader;
            Writer = channel.Writer;
        }
    }
}
