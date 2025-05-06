using Devices.Configs;
using Devices.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using Shared.Abstractions;
using Shared.Communication;
using Shared.Configs;
using Shared.Models;
using System.Text.Json;
using static Devices.Enums.GlobalEnums;

namespace Devices.Simulator
{
    public class DeviceSimulator
    {
        private readonly ILogger<DeviceSimulator> _logger;
        //private readonly MqttClientManager _mqttClientManager; // Inject the manager
        private readonly ICommunicator _communicator;
        private readonly MqttConfigs _mqttConfigs;
        private readonly DeviceConfigs _deviceConfigs;
        private readonly int _simulationIntervalMs;

        public DeviceSimulator(
            ILogger<DeviceSimulator> logger,
            IOptions<MqttConfigs> mqttConfigs, // Inject AppSettings
            IOptions<DeviceConfigs> deviceConfigs,
            //MqttClientManager mqttClientManager, // Inject MqttClientManager
            ICommunicator communicator // Inject MqttClientManager
            )
        {
            _logger = logger;
            _mqttConfigs = mqttConfigs.Value; // Store all settings
            _deviceConfigs = deviceConfigs.Value; // Store all settings
            //_mqttClientManager = mqttClientManager;
            _communicator = communicator;
            _simulationIntervalMs = _deviceConfigs.SimulationIntervalSeconds * 1000;
        }

        public async Task RunAsync(Device device, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting simulator for Device ID: {DeviceId}, Name: {DeviceName}, Type: {DeviceType}", device.Id, device.Name, device.SensorType);
            var random = new Random();

            

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // 1. Simulate Data Generation
                    double value = SimulateSensorValue(device.SensorType, random);

                    // 2. Prepare MQTT Message
                    var data = new Sensor
                    {
                        Id = device.Id,
                        Name = device.Name,
                        Type = device.SensorType,
                        Value = value,
                        Timestamp = DateTime.Now,
                        Location = "Inside",
                        Unit = device.SensorType == (int)DeviceType.Temperature ? "°C" : "%"
                    };

                    //string jsonPayload = JsonSerializer.Serialize(dataPayload);
                    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(data);

                    // Construct topic using format from settings
                    string topic = string.Format(_deviceConfigs.PublishNodesRawDataTopic, device.Id);

                    // 3. Publish via MQTT Client Manager
                    // Using AtMostOnce (0) for typical sensor data is often fine
                    //await _mqttClientManager.PublishAsync(topic, payload, qos: MqttQualityOfServiceLevel.AtMostOnce);
                    await _communicator.PublishAsync(topic, payload);

                    // Original logging (optional, maybe reduce verbosity now it's on MQTT)
                    // _logger.LogInformation("Device ID: {DeviceId} ({DeviceType}) | Value: {Value}", device.Id, device.Type, value);
                    _logger.LogDebug($"Device ID: {device.Id} published {value} to {topic}");

                    // 4. Wait for the next interval or cancellation
                    await Task.Delay(_simulationIntervalMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Simulator stopped gracefully for Device ID: {DeviceId}, Name: {DeviceName}", device.Id, device.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in simulator loop for Device ID: {DeviceId}, Name: {DeviceName}", device.Id, device.Name);
            }
            finally
            {
                _logger.LogDebug("Simulator task ending for Device ID: {DeviceId}", device.Id);
                // Optional: Publish an 'offline' status message? Requires retain=true
                // string offlinePayload = JsonSerializer.Serialize(new { status = "offline", timestamp = DateTime.UtcNow });
                // string statusTopic = $"status/devices/{device.Id}"; // Example status topic
                // await _mqttClientManager.PublishAsync(statusTopic, offlinePayload, retain: true);
            }
        }

        private double SimulateSensorValue(int deviceType, Random random)
        {
            return deviceType switch
            {
                (int)DeviceType.Temperature => Math.Round(random.NextDouble() * 10 + 50, 2),     // Simulate 5.00 - 35.00 C
                (int)DeviceType.Humidity => Math.Round(random.NextDouble() * 60 + 30, 2),     // Simulate 30.00 - 90.00 %
                (int)DeviceType.SoilMoisture => Math.Round(random.NextDouble() * 1 + 100, 0), // Simulate 300 - 1000 (raw value)
                _ => -1,
            };
        }

        // --- Placeholder for Command Handling (See notes below) ---
        private void ProcessCommand(byte[] command)
        {
            Console.WriteLine("Process command sucessfully!");

            _logger.LogInformation("Device ID {DeviceId} received command: {Command}", /* Need deviceId here */ command);
            // TODO: Implement command parsing and action logic here
            // e.g., if (commandPayload == "stop") { /* signal cancellation? */ }
            // e.g., if (commandPayload.StartsWith("setInterval:")) { /* parse and update _simulationIntervalMs */ }
        }


    }
}
