using Gateway.Configs;
using Gateway.Models;
using Microsoft.Extensions.Options;
using MQTTnet.Protocol;
using Shared.Abstractions;
using Shared.Communication;
using System.Runtime;

namespace Gateway.Background
{
    public class GatewayWokerService : BackgroundService
    {
        private readonly ILogger<GatewayWokerService> _logger;
        private readonly ICommunicator _communicator;
        private readonly GatewayConfigs _gatewayConfigs;
        private readonly ProcessingChannel _processingChannel;
        public GatewayWokerService(
            ILogger<GatewayWokerService> logger,
            ICommunicator mqttCommunicator,
            IOptions<GatewayConfigs> gatewayConfigs,
            ProcessingChannel processingChannel)
        {
            _logger = logger;
            _communicator = mqttCommunicator;
            _gatewayConfigs = gatewayConfigs.Value;
            _processingChannel = processingChannel;

            if (string.IsNullOrWhiteSpace(_gatewayConfigs.SubcribeNodesDataProcessedTopic) || string.IsNullOrWhiteSpace(_gatewayConfigs.ServerBaseUrl))
            {
                throw new InvalidOperationException("NodeDataTopicPattern and ServerIngestUrl must be configured in appsettings.json");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Simple Gateway Worker starting.");

            _communicator.MessageReceived += OnMessageReceived;
            _communicator.ConnectionStatusChanged += OnConnectionStatusChanged;

            try
            {
                await _communicator.ConnectAsync(stoppingToken);

                // Simple delay to allow connection before subscribing
                await Task.Delay(2000, stoppingToken);

                if (_communicator.IsConnected)
                {
                    await _communicator.SubscribeAsync(_gatewayConfigs.SubcribeNodesDataProcessedTopic, MqttQualityOfServiceLevel.AtLeastOnce, stoppingToken);

                }
                else
                {
                    _logger.LogWarning("MQTT client not connected. Will attempt subscription upon connection.");
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
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Gateway worker stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unhandled exception in GatewayWorker.");
            }
            finally
            {
                _communicator.MessageReceived -= OnMessageReceived;
                _communicator.ConnectionStatusChanged -= OnConnectionStatusChanged;
            }
        }

        private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
        {
            _logger.LogInformation("MQTT Connection Status Changed: IsConnected = {IsConnected}, Reason = {Reason}", e.IsConnected, e.Reason ?? "N/A");
            // Trigger application logic based on connection status (e.g., enable/disable features)
        }

        // --- Keep this handler FAST ---
        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            _logger.LogDebug("Gateway {GatewayId}: Message received on topic {Topic}", _gatewayConfigs.GatewayId, e.Topic);
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
                    _logger.LogWarning("Gateway {GatewayId}: Enqueuing message for topic {Topic} encountered backpressure or delay.", _gatewayConfigs.GatewayId, e.Topic);
                    // Monitor the task completion asynchronously if needed, but don't await here
                    writeTask.AsTask().ContinueWith(t => {
                        if (t.IsFaulted) _logger.LogError(t.Exception, "Gateway {GatewayId}: Failed to write message for topic {Topic} to channel.", _gatewayConfigs.GatewayId, e.Topic);
                    });
                }
                // DO NOT do heavy processing (parsing, DB calls, complex logic) here!

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message received on topic {Topic}", e.Topic);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Simple Gateway Worker stopping.");
            _processingChannel.Writer.TryComplete(); // Signal forwarding service to stop
            return base.StopAsync(cancellationToken);
        }

    }
}
