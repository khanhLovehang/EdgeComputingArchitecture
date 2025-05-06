using MQTTnet.Protocol;
using Shared.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Communication
{
    public interface ICommunicator : IAsyncDisposable
    {
        /// <summary>
        /// Event triggered when a message is received on a subscribed topic.
        /// </summary>
        event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Event triggered when the connection status to the broker changes.
        /// </summary>
        event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;

        /// <summary>
        /// Gets a value indicating whether the client is currently connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connects to the communication broker.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnects from the communication broker.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes a message to a specific topic.
        /// </summary>
        /// <param name="topic">The target topic.</param>
        /// <param name="payload">The message payload.</param>
        /// <param name="qos">Quality of Service level.</param>
        /// <param name="retain">Retain flag.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, bool retain = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes to a topic filter.
        /// </summary>
        /// <param name="topicFilter">The topic filter to subscribe to.</param>
        /// <param name="qos">The desired Quality of Service level.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unsubscribes from a topic filter.
        /// </summary>
        /// <param name="topicFilter">The topic filter to unsubscribe from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default);
    }
}
