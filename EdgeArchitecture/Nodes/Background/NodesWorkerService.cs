using Microsoft.Extensions.Options;
using MQTTnet.Protocol;
using Nodes.Configs;
using Nodes.Models;
using Nodes.ProcessData;
using Shared.Abstractions;
using Shared.Communication;

namespace Nodes.Background
{
    public class NodesWorkerService : BackgroundService
    {
        private readonly ILogger<NodesWorkerService> _logger;
        private readonly NodeConfigs _nodeConfigs;
        private readonly ICommunicator _communicator;
        private readonly IDataProcessor _dataProcessor;
        private readonly ProcessingChannel _processingChannel;
        public NodesWorkerService(ILogger<NodesWorkerService> logger
            , IOptions<NodeConfigs> nodeConfigs
            , ICommunicator communicator
            , IDataProcessor dataProcessor
            , ProcessingChannel processingChannel
            )
        {
            _logger = logger;
            _nodeConfigs = nodeConfigs.Value;
            _communicator = communicator;
            _dataProcessor = dataProcessor;
            _processingChannel = processingChannel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Subscribe to events
            _communicator.MessageReceived += OnMessageReceived;
            _communicator.ConnectionStatusChanged += OnConnectionStatusChanged;

            try
            {
                // Connect the communicator (it might auto-connect depending on design, but explicit is often clearer)
                _logger.LogInformation("Attempting to connect MQTT communicator...");
                await _communicator.ConnectAsync(stoppingToken); // Connect on startup

                // Subscribe topics
                // Wait until connected to subscribe (or let ManagedClient handle resubscribe)
                // A short delay or waiting for the first Connected event might be robust.
                await Task.Delay(2000, stoppingToken); // Simple delay, better: wait for IsConnected flag or event
                if (_communicator.IsConnected)
                {
                    // --- Device Data ---
                    // ** CRITICAL FOR SCALABILITY: Use Shared Subscription if possible **
                    // Example: $share/groupName/devices/+/data
                    await _communicator.SubscribeAsync(_nodeConfigs.SubcribeDevicesDataTopic, MqttQualityOfServiceLevel.AtLeastOnce, stoppingToken);

                    // --- Gateway Commands (Node Specific) ---
                    await _communicator.SubscribeAsync(_nodeConfigs.SubcribeGatewayCommandTopic, MqttQualityOfServiceLevel.AtLeastOnce, stoppingToken);


                    await _communicator.SubscribeAsync("esp32/temp/warning", MqttQualityOfServiceLevel.AtLeastOnce, stoppingToken);

                    // --- Gateway Config (All Nodes) ---
                }
                else
                {
                    _logger.LogWarning("MQTT client not connected after initial connect attempt. Subscriptions will be attempted upon connection.");
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_communicator.IsConnected)
                    {
                        try
                        {
                            // Optional: Perform periodic tasks if needed (e.g., health checks, publish node status)
                            // Example: Publish node heartbeat
                            // string heartbeatTopic = $"nodes/{_nodeId}/heartbeat";
                            // await _communicator.PublishAsync(heartbeatTopic, System.Text.Encoding.UTF8.GetBytes($"{{\"timestamp\":\"{DateTime.UtcNow:O}\"}}"), MqttQualityOfServiceLevel.AtMostOnce, false, stoppingToken);

                            await Task.Delay(TimeSpan.FromMinutes(1000), stoppingToken);
                            //await Task.Delay(3000, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            _logger.LogInformation("Publish loop cancelled.");
                            break; // Exit loop cleanly on cancellation
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to publish status update.");
                            // Decide how to handle publish errors (retry, log, etc.)
                        }
                    }
                    else
                    {
                        _logger.LogWarning("MQTT communicator is not connected. Skipping status publish.");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (Exception)
            {

                throw;
            }
            await Task.Delay(1000);
        }

        private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
        {
            _logger.LogInformation("MQTT Connection Status Changed: IsConnected = {IsConnected}, Reason = {Reason}", e.IsConnected, e.Reason ?? "N/A");
            // Trigger application logic based on connection status (e.g., enable/disable features)
        }

        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            _logger.LogDebug("Node {NodeId}: Message received on topic {Topic}", _nodeConfigs.NodeId, e.Topic);
            try
            {
                // Create a copy of the payload immediately if the underlying buffer might be reused
                var payloadCopy = e.Payload.ToArray(); // Or use memory pooling if performance critical

                var queuedMessage = new QueuedMessage(e.Topic, payloadCopy);

                // Try to write to the channel asynchronously, but don't wait here
                // Use TryWrite for a non-blocking attempt if the channel might be full and you'd rather drop messages
                // Use WriteAsync if you need backpressure (will wait if channel is full)
                ValueTask writeTask = _processingChannel.Writer.WriteAsync(queuedMessage); // Don't block/await

                if (!writeTask.IsCompletedSuccessfully)
                {
                    // Log if write is slow or stalls - indicates processing backlog
                    _logger.LogWarning("Node {NodeId}: Enqueuing message for topic {Topic} encountered backpressure or delay.", _nodeConfigs.NodeId, e.Topic);
                    // Monitor the task completion asynchronously if needed, but don't await here
                    writeTask.AsTask().ContinueWith(t => {
                        if (t.IsFaulted) _logger.LogError(t.Exception, "Node {NodeId}: Failed to write message for topic {Topic} to channel.", _nodeConfigs.NodeId, e.Topic);
                    });
                }
                // DO NOT do heavy processing (parsing, DB calls, complex logic) here!

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message received on topic {Topic}", e.Topic);
            }
        }
    }
}
