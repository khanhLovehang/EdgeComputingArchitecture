using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet;
using Shared.Abstractions;
using Shared.Configs;

namespace Shared.Communication
{
    public class MqttCommunicator : ICommunicator // Implement IAsyncDisposable correctly
    {
        private readonly ILogger<MqttCommunicator> _logger;
        private readonly MqttConfigs _mqttConfigs;
        private IManagedMqttClient? _managedMqttClient;
        private readonly MqttFactory _mqttFactory;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed = false;
        private bool _explicitDisconnect = false;

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        public bool IsConnected => _managedMqttClient?.IsConnected ?? false;

        public MqttCommunicator(IOptions<MqttConfigs> options, ILogger<MqttCommunicator> logger)
        {
            _mqttConfigs = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mqttFactory = new MqttFactory(); // Use logger factory if available: new MqttFactory(logger)

            // Validate options
            if (string.IsNullOrWhiteSpace(_mqttConfigs.BrokerAddress))
                throw new ArgumentException("BrokerAddress cannot be empty.", nameof(options));
            if (string.IsNullOrWhiteSpace(_mqttConfigs.ClientId))
                throw new ArgumentException("ClientId cannot be empty.", nameof(options));

            InitializeMqttClient();
        }

        private void InitializeMqttClient()
        {
            _managedMqttClient = _mqttFactory.CreateManagedMqttClient();

            // --- Event Handlers ---
            _managedMqttClient.ConnectedAsync += OnConnected;
            _managedMqttClient.DisconnectedAsync += OnDisconnected;
            _managedMqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
            _managedMqttClient.ConnectingFailedAsync += OnConnectingFailed;
            // Consider adding handlers for SynchronizingSubscriptionsFailedAsync etc. for more robustness
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MqttCommunicator));
            if (IsConnected)
            {
                _logger.LogInformation("Already connected to MQTT broker at {BrokerAddress}:{Port}", _mqttConfigs.BrokerAddress, _mqttConfigs.Port);
                return;
            }

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (IsConnected) return; // Double check after lock

                _logger.LogInformation("Connecting to MQTT broker at {BrokerAddress}:{Port} with ClientId {ClientId}...",
                    _mqttConfigs.BrokerAddress, _mqttConfigs.Port, _mqttConfigs.ClientId);

                _explicitDisconnect = false; // Reset flag on connect attempt

                var clientOptionsBuilder = new MqttClientOptionsBuilder()
                    .WithClientId(_mqttConfigs.ClientId)
                    .WithTcpServer(_mqttConfigs.BrokerAddress, _mqttConfigs.Port)
                    .WithCleanSession(true) // Typically true for ManagedClient unless specific state needed
                    .WithKeepAlivePeriod(_mqttConfigs.KeepAlivePeriod);

                // Credentials
                if (!string.IsNullOrWhiteSpace(_mqttConfigs.Username))
                {
                    clientOptionsBuilder.WithCredentials(_mqttConfigs.Username, _mqttConfigs.Password);
                }

                // TLS
                if (_mqttConfigs.UseTls)
                {
                    //var tlsOptions = new MqttClientOptionsBuilderTlsParameters
                    var tlsOptions = new MqttClientTlsOptions
                    {
                        UseTls = true,
                        AllowUntrustedCertificates = _mqttConfigs.TlsAllowUntrustedCertificates,
                        IgnoreCertificateRevocationErrors = _mqttConfigs.TlsIgnoreCertificateRevocationErrors,
                        IgnoreCertificateChainErrors = _mqttConfigs.TlsIgnoreCertificateChainErrors
                        // Configure Certificates here if needed (e.g., for mTLS)
                        // Example: .WithCertificateValidationHandler(TlsValidationHandler) // Custom validation
                    };
                    clientOptionsBuilder.WithTlsOptions(tlsOptions);
                }

                // LWT (Last Will and Testament)
                if (_mqttConfigs.UseLwt && !string.IsNullOrWhiteSpace(_mqttConfigs.LwtTopic))
                {
                    var lwtMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(_mqttConfigs.LwtTopic)
                        .WithPayload(_mqttConfigs.LwtPayload ?? Array.Empty<byte>())
                        .WithQualityOfServiceLevel(_mqttConfigs.LwtQos)
                        .WithRetainFlag(_mqttConfigs.LwtRetain)
                        .Build();
                    //clientOptionsBuilder.WithWillMessage(lwtMessage);
                    //clientOptionsBuilder.WithWillMessageExpiryInterval(lwtMessage);
                    _logger.LogInformation("LWT configured for topic {LwtTopic}", _mqttConfigs.LwtTopic);
                }


                var managedClientOptions = new ManagedMqttClientOptionsBuilder()
                    .WithAutoReconnectDelay(_mqttConfigs.ReconnectDelay)
                    .WithClientOptions(clientOptionsBuilder.Build())
                    .Build();

                await _managedMqttClient!.StartAsync(managedClientOptions);
                // Note: Managed client starts connecting in the background.
                // The ConnectedAsync event confirms successful connection.

                _logger.LogInformation("Managed MQTT client started. Waiting for connection...");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Managed MQTT client connection to {BrokerAddress}:{Port}", _mqttConfigs.BrokerAddress, _mqttConfigs.Port);
                // Consider cleanup or re-throwing based on requirements
                throw; // Re-throw maybe needed for caller to know connection failed immediately
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed) return;

            _logger.LogInformation("Disconnecting MQTT client explicitly...");
            _explicitDisconnect = true; // Mark as intentional disconnect

            if (_managedMqttClient != null)
            {
                try
                {
                    // Give some time for potential queued messages to be sent
                    await _managedMqttClient.StopAsync(cleanDisconnect: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception occurred during MQTT client stop.");
                }
            }
            // ConnectionStatusChanged event will be triggered by the OnDisconnected handler
        }

        public async Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, bool retain = false, CancellationToken cancellationToken = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MqttCommunicator));
            if (_managedMqttClient == null || !_managedMqttClient.IsStarted)
            {
                _logger.LogWarning("Cannot publish. MQTT Client is not started/initialized.");
                // Optionally throw an exception: throw new InvalidOperationException("Client not started.");
                return; // Or enqueue locally if offline queueing beyond ManagedClient is needed
            }
            if (!IsConnected && _managedMqttClient.PendingApplicationMessagesCount > 1000) // Example Threshold
            {
                _logger.LogWarning("Client is not connected and offline queue is large ({Count}). Skipping publish to topic {Topic}.",
                    _managedMqttClient.PendingApplicationMessagesCount, topic);
                // Maybe throw, maybe just log, depends on requirements
                return;
            }


            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(qos)
                    .WithRetainFlag(retain)
                    .Build();

                // EnqueueAsync handles offline queueing automatically
                await _managedMqttClient.EnqueueAsync(message);

                _logger.LogDebug("Enqueued message for topic {Topic} with QoS {Qos} (Retain: {Retain})", topic, qos, retain);
            }
            catch (MqttCommunicationException ex) // Catch specific MQTT exceptions
            {
                _logger.LogError(ex, "MQTT Communication Error publishing to topic {Topic}.", topic);
                // Potentially trigger a reconnect or specific error handling
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to topic {Topic}.", topic);
                // Rethrow or handle as appropriate
                throw;
            }
        }

        public async Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken cancellationToken = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MqttCommunicator));
            if (_managedMqttClient == null || !_managedMqttClient.IsStarted)
            {
                _logger.LogWarning("Cannot subscribe. MQTT Client is not started/initialized.");
                // Optionally throw an exception: throw new InvalidOperationException("Client not started.");
                return;
            }

            try
            {
                _logger.LogInformation("Subscribing to topic filter {TopicFilter} with QoS {Qos}", topicFilter, qos);
                var subscriptionOptions = _mqttFactory.CreateTopicFilterBuilder()
                    .WithTopic(topicFilter)
                    .WithQualityOfServiceLevel(qos)
                    .Build();

                // Managed client handles retrying subscriptions on reconnect
                await _managedMqttClient.SubscribeAsync(new List<MqttTopicFilter> { subscriptionOptions });
            }
            catch (MqttCommunicationException ex)
            {
                _logger.LogError(ex, "MQTT Communication Error subscribing to topic filter {TopicFilter}.", topicFilter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to topic filter {TopicFilter}.", topicFilter);
                throw;
            }
        }

        public async Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MqttCommunicator));
            if (_managedMqttClient == null || !_managedMqttClient.IsStarted)
            {
                _logger.LogWarning("Cannot unsubscribe. MQTT Client is not started/initialized.");
                return;
            }

            try
            {
                _logger.LogInformation("Unsubscribing from topic filter {TopicFilter}", topicFilter);
                await _managedMqttClient.UnsubscribeAsync(new List<string> { topicFilter });
            }
            catch (MqttCommunicationException ex)
            {
                _logger.LogError(ex, "MQTT Communication Error unsubscribing from topic filter {TopicFilter}.", topicFilter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from topic filter {TopicFilter}.", topicFilter);
                throw;
            }
        }

        // --- Event Handlers ---

        private Task OnConnected(MqttClientConnectedEventArgs e)
        {
            _logger.LogInformation("Successfully connected to MQTT broker. ResultCode: {ResultCode}", e.ConnectResult.ResultCode);
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(true));

            // Re-subscribe to initial topics if needed (ManagedClient often handles this, but explicit can be safer depending on config)
            if (_mqttConfigs.InitialSubscriptions != null && _mqttConfigs.InitialSubscriptions.Any())
            {
                _logger.LogInformation("Subscribing to initial topics...");
                var tasks = _mqttConfigs.InitialSubscriptions.Select(sub =>
                    SubscribeAsync(sub.Key, sub.Value) // Use the public method to ensure logging/error handling
                        .ContinueWith(t => { if (t.IsFaulted) _logger.LogError(t.Exception, "Failed initial subscription to {Topic}", sub.Key); })
                );
                // Don't await here to avoid blocking the event handler, let them run in background
                Task.WhenAll(tasks);
            }

            return Task.CompletedTask;
        }

        private Task OnDisconnected(MqttClientDisconnectedEventArgs e)
        {
            // Log differently based on whether it was an explicit disconnect or an unexpected one
            if (_explicitDisconnect)
            {
                _logger.LogInformation("Disconnected from MQTT broker explicitly.");
            }
            else
            {
                _logger.LogWarning(e.Exception, "Disconnected from MQTT broker unexpectedly. Reason: {Reason}. Will attempt to reconnect.", e.ReasonString);
            }

            // Pass the reason if available and not an explicit disconnect
            string? reason = _explicitDisconnect ? null : e.ReasonString;
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(false, reason));

            // Managed client handles reconnection attempts automatically based on options.
            return Task.CompletedTask;
        }

        private Task OnConnectingFailed(ConnectingFailedEventArgs e)
        {
            Console.WriteLine("OnConnectingFailed.");
            _logger.LogError(e.Exception, "Failed to connect to MQTT broker. Exception: {Exception}, Result: {ResultCode}", e.Exception, e.ConnectResult?.ResultCode);
            // ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(false, $"Connection failed: {e.ConnectResult?.ResultCode}")); // Optional: signal failed attempt status
            return Task.CompletedTask;
        }

        private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                // Prevent processing if no subscribers
                if (MessageReceived == null) return Task.CompletedTask;

                e.AutoAcknowledge = true; // Or set to false if you need complex ACK logic

                _logger.LogDebug("Received message on topic {Topic} (QoS: {Qos}, Retain: {Retain}). Payload size: {Size} bytes",
                                e.ApplicationMessage.Topic,
                                e.ApplicationMessage.QualityOfServiceLevel,
                                e.ApplicationMessage.Retain,
                                e.ApplicationMessage.PayloadSegment.Count);

                // Consider running the handler in a background thread if processing is long
                // Task.Run(() => { ... });
                // Or use a dedicated message processing queue

                // Create a copy of the payload if the buffer might be reused by MQTTnet
                var payloadCopy = e.ApplicationMessage.PayloadSegment.ToArray();

                var eventArgs = new MessageReceivedEventArgs(
                    e.ApplicationMessage.Topic,
                    payloadCopy,
                    e.ApplicationMessage.QualityOfServiceLevel,
                    e.ApplicationMessage.Retain
                );

                MessageReceived?.Invoke(this, eventArgs);

                // Log after invoking if necessary
                _logger.LogTrace("Message handler invoked for topic {Topic}", e.ApplicationMessage.Topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing received message from topic {Topic}.", e.ApplicationMessage.Topic);
                // Decide if you need to Nack the message if AutoAcknowledge was false
                // e.ProcessingFailed = true; // If AutoAcknowledge = false
            }
            return Task.CompletedTask;
        }

        // --- Dispose Pattern ---
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _logger.LogInformation("Disposing MqttCommunicator...");

                if (_managedMqttClient != null)
                {
                    // Unsubscribe event handlers to prevent memory leaks
                    _managedMqttClient.ConnectedAsync -= OnConnected;
                    _managedMqttClient.DisconnectedAsync -= OnDisconnected;
                    _managedMqttClient.ApplicationMessageReceivedAsync -= OnMessageReceived;
                    _managedMqttClient.ConnectingFailedAsync -= OnConnectingFailed;

                    if (_managedMqttClient.IsStarted)
                    {
                        await DisconnectAsync(); // Attempt a clean disconnect
                    }
                    _managedMqttClient.Dispose();
                    _managedMqttClient = null;
                }
                _connectionLock.Dispose();
                _logger.LogInformation("MqttCommunicator disposed.");
            }
        }

        ~MqttCommunicator()
        {
            // Finalizer calls Dispose(false)
            DisposeAsyncCore().ConfigureAwait(false).GetAwaiter().GetResult(); // Not ideal, but ensures resources are released if DisposeAsync wasn't called
        }
    }
}
