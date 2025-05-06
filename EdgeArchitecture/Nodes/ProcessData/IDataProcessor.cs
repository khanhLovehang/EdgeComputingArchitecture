using Nodes.Models;

namespace Nodes.ProcessData
{
    public interface IDataProcessor
    {
        /// <summary>
        /// Determines if this processor can handle a message from the given topic.
        /// </summary>
        bool CanProcess(string topic);

        /// <summary>
        /// Processes the queued message asynchronously.
        /// </summary>
        Task ProcessMessageAsync(QueuedMessage message, CancellationToken cancellationToken);
    }
}
