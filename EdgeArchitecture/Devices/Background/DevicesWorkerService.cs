using Devices.Configs;
using Devices.Models;
using Devices.Services;
using Devices.Simulator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet.Protocol;
using Shared.Abstractions;
using Shared.Communication;
using Shared.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Devices.Background
{
    public class DevicesWorkerService : BackgroundService
    {
        private readonly ILogger<DevicesWorkerService> _logger;
        private readonly ICommunicator _communicator;
        private readonly IDeviceServices _deviceService;
        private readonly DeviceConfigs _deviceConfigs;
        private readonly IServiceProvider _serviceProvider;

        // Store running simulators: Key = DeviceId, Value = CancellationTokenSource
        private readonly ConcurrentDictionary<long, CancellationTokenSource> _runningSimulators = new();
        // Store the task associated with the simulator to potentially await its completion on stop
        private readonly ConcurrentDictionary<long, Task> _simulatorTasks = new();

        // Inject the ICommunicator
        public DevicesWorkerService(
            ILogger<DevicesWorkerService> logger
            , ICommunicator communicator
            , IDeviceServices deviceService
            , IOptions<DeviceConfigs> deviceConfigs
            , IServiceProvider serviceProvider
            )
        {
            _logger = logger;
            _communicator = communicator;
            _deviceService = deviceService;
            _deviceConfigs = deviceConfigs.Value;
            _serviceProvider = serviceProvider;
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

                await _communicator.SubscribeAsync(_deviceConfigs.SubcribeNodesCommandTopic);
                await _communicator.SubscribeAsync(_deviceConfigs.SubcribeNodesAlertTopic);

                // Example: Periodically publish status
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_communicator.IsConnected)
                    {
                        try
                        {
                            //string statusPayload = $"{{ \"timestamp\": \"{DateTime.UtcNow:O}\", \"status\": \"online\" }}";
                            //byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(statusPayload);
                            // Use clientId from options if needed for the topic, requires accessing IOptions<MqttOptions> or storing clientId
                            //await _communicator.PublishAsync($"device/some-id/status", payloadBytes, MqttQualityOfServiceLevel.AtLeastOnce, false, stoppingToken);
                            // Get the desired state from the API
                            List<Device> desiredDevices = await _deviceService.GetDevicesAsync(stoppingToken);

                            // Get the current state (running simulators)
                            List<long> runningDeviceIds = _runningSimulators.Keys.ToList();

                            // --- Synchronization Logic ---

                            // 1. Stop simulators for devices that are removed or inactive
                            List<long> devicesToStop = runningDeviceIds
                                .Where(id => !desiredDevices.Any(d => d.Id == id && d.IsActive))
                                .ToList();

                            foreach (var deviceId in devicesToStop)
                            {
                                await StopSimulatorAsync(deviceId);
                            }

                            // 2. Start simulators for new or newly activated devices
                            List<Device> devicesToStart = desiredDevices
                                .Where(d => d.IsActive && !_runningSimulators.ContainsKey(d.Id))
                                .ToList();

                            foreach (var device in devicesToStart)
                            {
                                StartSimulator(device, stoppingToken); // Pass the main stopping token
                            }

                            _logger.LogDebug("Device check complete. Running simulators: {Count}", _runningSimulators.Count);
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
                _logger.LogInformation("Worker service is stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred in the worker service execute loop.");
            }
            finally
            {
                _logger.LogInformation("Worker service shutting down. Cleaning up communicator...");
                // Unsubscribe from events
                _communicator.MessageReceived -= OnMessageReceived;
                _communicator.ConnectionStatusChanged -= OnConnectionStatusChanged;

                // Explicitly disconnect if the communicator doesn't handle it in Dispose or if desired
                // await _communicator.DisconnectAsync(); // Often handled by DisposeAsync or host shutdown

                // If ICommunicator is IAsyncDisposable and registered as Singleton, the host should dispose it.
                // If ICommunicator is IDisposable, the host should dispose it.
            }
        }

        private void StartSimulator(Device device, CancellationToken stoppingToken)
        {
            if (_runningSimulators.ContainsKey(device.Id)) return; // Already running (shouldn't happen with current logic but safe check)

            _logger.LogInformation("Requesting start for Device ID: {DeviceId}, Name: {Name}", device.Id, device.Name);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken); // Link to the main stop token

            if (_runningSimulators.TryAdd(device.Id, cts))
            {
                // Resolve a simulator instance - assuming it's registered as transient or scoped
                // If DeviceSimulator is singleton, you might need a factory or redesign it to be stateless
                var simulator = _serviceProvider.GetRequiredService<DeviceSimulator>();

                // Start the simulation task without awaiting it here
                Task simulatorTask = Task.Run(() => simulator.RunAsync(device, cts.Token), cts.Token);
                _simulatorTasks.TryAdd(device.Id, simulatorTask); // Store the task

                // Optional: Add a continuation to remove the task from the dictionary when it completes/faults/cancels
                simulatorTask.ContinueWith(t =>
                {
                    _simulatorTasks.TryRemove(device.Id, out _);
                    _runningSimulators.TryRemove(device.Id, out var removedCts);
                    removedCts?.Dispose(); // Dispose CTS when task finishes
                    if (t.IsFaulted)
                    {
                        _logger.LogError(t.Exception, "Simulator task for Device ID {DeviceId} faulted.", device.Id);
                    }
                    else if (t.IsCanceled)
                    {
                        _logger.LogWarning("Simulator task for Device ID {DeviceId} was canceled.", device.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Simulator task for Device ID {DeviceId} completed.", device.Id);
                    }
                }, TaskScheduler.Default); // Use default scheduler
            }
            else
            {
                _logger.LogWarning("Failed to add CancellationTokenSource for Device ID: {DeviceId}. Already exists?", device.Id);
                cts.Dispose(); // Dispose if not added
            }
        }
        private async Task StopSimulatorAsync(long deviceId)
        {
            if (_runningSimulators.TryRemove(deviceId, out var cts))
            {
                _logger.LogInformation("Requesting stop for Device ID: {DeviceId}", deviceId);
                cts.Cancel(); // Signal cancellation

                // Optionally wait for the task to finish, but with a timeout
                if (_simulatorTasks.TryRemove(deviceId, out var task))
                {
                    try
                    {
                        await task.WaitAsync(TimeSpan.FromSeconds(5)); // Wait max 5 seconds for graceful shutdown
                        _logger.LogDebug("Simulator task for Device ID {DeviceId} finished after stop request.", deviceId);
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("Simulator task for Device ID {DeviceId} did not finish within timeout after cancellation.", deviceId);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogDebug("Simulator task for Device ID {DeviceId} correctly cancelled.", deviceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception while waiting for simulator task {DeviceId} to stop.", deviceId);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not find associated task for Device ID {DeviceId} during stop.", deviceId);
                }

                cts.Dispose(); // Dispose the CancellationTokenSource
                _logger.LogInformation("Stopped simulator for Device ID: {DeviceId}", deviceId);
            }
        }

        private async Task StopAllSimulatorsAsync()
        {
            var stopTasks = new List<Task>();
            var deviceIds = _runningSimulators.Keys.ToList(); // Get keys before iterating

            foreach (var deviceId in deviceIds)
            {
                // Don't wait individually here, just initiate stop and collect tasks
                stopTasks.Add(StopSimulatorAsync(deviceId));
            }
            await Task.WhenAll(stopTasks); // Wait for all stop operations to complete
            _logger.LogInformation("All simulators have been requested to stop.");
        }

        private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
        {
            _logger.LogInformation("MQTT Connection Status Changed: IsConnected = {IsConnected}, Reason = {Reason}", e.IsConnected, e.Reason ?? "N/A");
            // Trigger application logic based on connection status (e.g., enable/disable features)
        }

        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            try
            {
                string payloadStr = System.Text.Encoding.UTF8.GetString(e.Payload);
                _logger.LogInformation("Received message on topic '{Topic}': {Payload}", e.Topic, payloadStr);
                var data = JsonSerializer.Deserialize<AlertData>(payloadStr);

                if (data != null)
                {
                    // --- Process the message based on the topic ---
                    if (e.Topic.ToLower().Contains("command")) // Example topic matching
                    {
                        //{ "deviceId":"2","sensorType":"Humidity","alert":"high_threshold","value":85.02,"threshold":85,"timestamp":"2025-04-25T17:18:38.0826224Z"}


                        // Handle command
                        ProcessCommand(data);
                    }
                    else if (e.Topic.ToLower().Contains("alert"))
                    {
                        // Handle configuration update
                        _logger.LogWarning($"[ALERT] {data.SensorType}{data.DeviceId} {data.Alert}. Threshold: {data.Threshold} - Value: {data.Value}");
                    }
                    else
                    {
                        _logger.LogWarning("Received message on unhandled topic: {Topic}", e.Topic);
                    }
                }
                else
                {
                    _logger.LogInformation("Received message on topic '{Topic}': {Payload}", e.Topic, payloadStr);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message received on topic {Topic}", e.Topic);
            }
        }

        private void ProcessCommand(AlertData alertData)
        {
            switch (alertData.SensorType.ToLower())
            {
                case "temperature":
                    if (alertData.Alert.ToLower().Contains("high_threshold"))
                    {
                        //_logger.LogWarning($"[ALERT] TemperatureSensor{alertData.DeviceId} alert high threshold. Threshold: {alertData.Threshold} - Value: {alertData.Value}");
                        _logger.LogWarning($"[COMMAND] TemperatureSensor{alertData.DeviceId} TURN ON air condition");
                    }

                    if (alertData.Alert.ToLower().Contains("low_threshold"))
                    {
                        //_logger.LogWarning($"[ALERT] TemperatureSensor{alertData.DeviceId} alert low threshold. Threshold: {alertData.Threshold} - Value: {alertData.Value}");
                        _logger.LogWarning($"[COMMAND] TemperatureSensor{alertData.DeviceId} TURN ON heating lamp");
                    }
                    break;
                case "humidity":

                    if (alertData.Alert.ToLower().Contains("high_threshold"))
                    {
                        //_logger.LogWarning($"[ALERT] HumiditySensor{alertData.DeviceId} alert high threshold. Threshold: {alertData.Threshold} - Value: {alertData.Value}");
                        _logger.LogWarning($"[COMMAND] HumiditySensor{alertData.DeviceId} TURN ON fan");
                    }

                    if (alertData.Alert.ToLower().Contains("low_threshold"))
                    {
                        //_logger.LogWarning($"[ALERT] HumiditySensor{alertData.DeviceId} alert low threshold. Threshold: {alertData.Threshold} - Value: {alertData.Value}");
                        _logger.LogWarning($"[COMMAND] HumiditySensor{alertData.DeviceId} TURN OFF fan");
                    }
                    break;
                case "soilmoisture":
                    if (alertData.Alert.ToLower().Contains("high_threshold"))
                    {
                        //_logger.LogWarning($"[ALERT] HumiditySensor{alertData.DeviceId} alert high threshold. Threshold: {alertData.Threshold} - Value: {alertData.Value}");
                        _logger.LogWarning($"[COMMAND] HumiditySensor{alertData.DeviceId} TURN OFF water");
                    }

                    if (alertData.Alert.ToLower().Contains("low_threshold"))
                    {
                        //_logger.LogWarning($"[ALERT] HumiditySensor{alertData.DeviceId} alert low threshold. Threshold: {alertData.Threshold} - Value: {alertData.Value}");
                        _logger.LogWarning($"[COMMAND] HumiditySensor{alertData.DeviceId} TURN ON water");
                    }
                    break;
                default:
                    break;
            }
            // Add command processing logic here
            // e.g., Parse JSON, trigger actions on the device
        }

        private void UpdateConfiguration(string payload)
        {
            _logger.LogInformation("Processing configuration update: {Config}", payload);
            // Add config update logic here
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StopAsync called on worker service.");
            // Perform any cleanup specific to the worker service before the host disposes dependencies
            await base.StopAsync(cancellationToken);
        }
    }
}
