using Microsoft.Extensions.Options;
using MQTTnet.Protocol;
using Nodes.Configs;
using Nodes.Helpers;
using Nodes.Models;
using Shared.Communication;
using Shared.Configs;
using Shared.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nodes.ProcessData
{
    public class SensorDataProcessor : IDataProcessor
    {
        private readonly ILogger<SensorDataProcessor> _logger;
        private readonly ProcessingConfigs _processingConfigs;
        private readonly MqttConfigs _mqttConfigs;
        private readonly NodeConfigs _nodeConfigs;
        private readonly ICommunicator _communicator; // To publish processed data
        private readonly Regex _deviceDataTopicRegex; // Precompile regex for efficiency

        // --- State for Aggregation (In-Memory) ---
        // Key: "{deviceId}:{sensorType}" (e.g., "Sensor01:Temperature")
        // Value: List of readings since last aggregation
        private readonly ConcurrentDictionary<string, List<double>> _aggregationBuffer = new();
        // Value: Timestamp of the last aggregation calculation for this key
        private readonly ConcurrentDictionary<string, DateTime> _lastAggregationTime = new();
        // Value: Tracks if an alert for a specific condition is currently active (to avoid flood)
        private readonly ConcurrentDictionary<string, bool> _activeAlerts = new();
        // --------------------------------------------

        public SensorDataProcessor(
            ILogger<SensorDataProcessor> logger,
            IOptions<ProcessingConfigs> processingConfigs, // Inject processing rules
            IOptions<MqttConfigs> mqttConfigs, // Inject MQTT configs for output topics
            IOptions<NodeConfigs> nodeConfigs, // Inject MQTT configs for output topics
            ICommunicator communicator)
        {
            _logger = logger;
            _processingConfigs = processingConfigs.Value;
            _mqttConfigs = mqttConfigs.Value;
            _nodeConfigs = nodeConfigs.Value;
            _communicator = communicator;

            // Create a Regex from the pattern to check topics and extract DeviceId
            // Example: devices/+/data -> devices/(.*?)/data
            // Example: $share/group/devices/+/data -> $share/.*?/devices/(.*?)/data
            string pattern = MqttTopicMatcher.ConvertWildcardsToRegex(_nodeConfigs.SubcribeDevicesDataTopic);
            _deviceDataTopicRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            _logger.LogInformation("DeviceDataProcessor initialized. Will match topics using Regex: {RegexPattern}", pattern);
        }

        public async Task ProcessMessageAsync(QueuedMessage message, CancellationToken cancellationTokene)
        {
            Sensor? sensorData = null;
            try
            {
                // 1. Parse Topic & Basic Validation
                sensorData = JsonSerializer.Deserialize<Sensor>(message.Payload);
                if (sensorData == null)
                {
                    //throw new JsonException("Deserialized payload is null."); 

                    _logger.LogWarning($"Deserialized payload is null. Payload: {message.Payload}");
                    return;
                }

                long deviceId = sensorData.Id;
                int sensorType = sensorData.Type; // e.g., "temperature"
                string sensorTypeString = "";
                switch (sensorType)
                {
                    case 1:
                        sensorTypeString = "Temperature";
                        break;
                    case 2:
                        sensorTypeString = "Humidity";
                        break;
                    case 3:
                        sensorTypeString = "SoilMoisture";
                        break;
                    default:
                        break;
                }
                string configKey = sensorTypeString;

                //// Check if we have processing rules for this sensor type
                if (!_processingConfigs.TryGetValue(configKey, out var config))
                {
                    _logger.LogTrace("No processing configuration found for sensor type '{SensorType}'. Forwarding raw data potentially?", configKey);
                    // Optionally forward raw data if needed, or just ignore
                    return;
                }

                // 3. Data Filtering & Cleaning
                if (!IsDataValid(sensorData.Value, config, message.Topic))
                {
                    return; // IsDataValid logs the reason
                }

                // 4. Threshold Monitoring (on Raw Data)
                await CheckThresholds(sensorData.Id, configKey, sensorData.Value, config, isAggregate: false);

                // 5. Data Aggregation
                await AggregateData(sensorData.Id, configKey, sensorData.Value, config);
            }
            catch (JsonException jsonEx)
            {
                //var rawPayload = Encoding.UTF8.GetString(payload);
                _logger.LogError(jsonEx, "Failed to parse JSON message from topic {Topic}. Payload: {Payload}", message.Topic, message.Payload);
                return;
            }


        }

        // --- Helper Methods ---
        private bool IsDataValid(double value, SensorProcessingConfig config, string topic)
        {
            // Check MinValue
            if (config.MinValue.HasValue && value < config.MinValue.Value)
            {
                _logger.LogWarning("FILTERED: Value {Value} from topic {Topic} is below MinValue {Min}", value, topic, config.MinValue.Value);
                return false;
            }
            // Check MaxValue
            if (config.MaxValue.HasValue && value > config.MaxValue.Value)
            {
                _logger.LogWarning("FILTERED: Value {Value} from topic {Topic} is above MaxValue {Max}", value, topic, config.MaxValue.Value);
                return false;
            }
            // Add other checks if needed (e.g., check for NaN)
            return true;
        }

        private async Task CheckThresholds(long deviceId, string sensorTypeKey, double value, SensorProcessingConfig config, bool isAggregate)
        {
            string alertTypeSuffix = isAggregate ? "_agg" : ""; // Differentiate raw vs aggregate alerts

            // Check High Threshold
            if (config.HighThreshold.HasValue && value > config.HighThreshold.Value)
            {
                string alertKey = $"{deviceId}:{sensorTypeKey}:high";
                // Publish alert only if not already active (simple debounce)
                if (_activeAlerts.TryAdd(alertKey, true)) // TryAdd is atomic
                {
                    _logger.LogWarning("ALERT: High threshold breached for {Device} {Sensor}. Value: {Value} > {Threshold}", deviceId, sensorTypeKey, value, config.HighThreshold.Value);
                    var alertPayload = JsonSerializer.SerializeToUtf8Bytes(new { DeviceId = deviceId, SensorType = sensorTypeKey, Alert = $"high_threshold{alertTypeSuffix}", Value = value, Threshold = config.HighThreshold.Value, Timestamp = DateTime.UtcNow });

                    // Publish alert for device
                    string alertTopic = string.Format(_nodeConfigs.PublishDevicesAlertTopic, deviceId, $"{sensorTypeKey.ToLower()}_high{alertTypeSuffix}");
                    await _communicator.PublishAsync(alertTopic, alertPayload, qos: MqttQualityOfServiceLevel.AtLeastOnce); // Use QoS 1 for alerts

                    // Publish command for device here
                    string commandTopic = string.Format(_nodeConfigs.PublishDevicesCommandTopic, deviceId, $"{sensorTypeKey.ToLower()}_high{alertTypeSuffix}");
                    await _communicator.PublishAsync(commandTopic, alertPayload, qos: MqttQualityOfServiceLevel.AtLeastOnce); // Use QoS 1 for alerts

                }
            }
            else // Value is below high threshold, clear active alert if it exists
            {
                string alertKey = $"{deviceId}:{sensorTypeKey}:high";
                if (_activeAlerts.TryRemove(alertKey, out _))
                {
                    _logger.LogInformation("CLEARED: High threshold alert for {Device} {Sensor}.", deviceId, sensorTypeKey);
                    // Optionally publish a 'cleared' message
                    var clearPayload = JsonSerializer.SerializeToUtf8Bytes(new { DeviceId = deviceId, SensorType = sensorTypeKey, Alert = $"high_threshold{alertTypeSuffix}_cleared", Timestamp = DateTime.UtcNow });

                    // Publish alert for device
                    string alertTopic = string.Format(_nodeConfigs.PublishDevicesAlertTopic, deviceId, $"{sensorTypeKey.ToLower()}_high{alertTypeSuffix}_cleared");
                    await _communicator.PublishAsync(alertTopic, clearPayload, qos: MqttQualityOfServiceLevel.AtLeastOnce);

                    // Publish command for device here
                    string commandTopic = string.Format(_nodeConfigs.PublishDevicesCommandTopic, deviceId, $"{sensorTypeKey.ToLower()}_high{alertTypeSuffix}");
                    await _communicator.PublishAsync(commandTopic, clearPayload, qos: MqttQualityOfServiceLevel.AtLeastOnce); // Use QoS 1 for alerts
                }
            }

            // Check Low Threshold (similar logic)
            if (config.LowThreshold.HasValue && value < config.LowThreshold.Value)
            {
                string alertKey = $"{deviceId}:{sensorTypeKey}:low";
                if (_activeAlerts.TryAdd(alertKey, true))
                {
                    _logger.LogWarning("ALERT: Low threshold breached for {Device} {Sensor}. Value: {Value} < {Threshold}", deviceId, sensorTypeKey, value, config.LowThreshold.Value);
                    var alertPayload = JsonSerializer.SerializeToUtf8Bytes(new { DeviceId = deviceId, SensorType = sensorTypeKey, Alert = $"low_threshold{alertTypeSuffix}", Value = value, Threshold = config.LowThreshold.Value, Timestamp = DateTime.UtcNow });

                    // Publish alert for device
                    string alertTopic = string.Format(_nodeConfigs.PublishDevicesAlertTopic, deviceId, $"{sensorTypeKey.ToLower()}_high{alertTypeSuffix}");
                    await _communicator.PublishAsync(alertTopic, alertPayload, qos: MqttQualityOfServiceLevel.AtLeastOnce); // Use QoS 1 for alerts

                    // Publish command for device here
                    string commandTopic = string.Format(_nodeConfigs.PublishDevicesCommandTopic, deviceId, $"{sensorTypeKey.ToLower()}_high{alertTypeSuffix}");
                    await _communicator.PublishAsync(commandTopic, alertPayload, qos: MqttQualityOfServiceLevel.AtLeastOnce); // Use QoS 1 for alerts

                }
            }
            else // Value is above low threshold, clear active alert
            {
                string alertKey = $"{deviceId}:{sensorTypeKey}:low";
                if (_activeAlerts.TryRemove(alertKey, out _))
                {
                    _logger.LogInformation("CLEARED: Low threshold alert for {Device} {Sensor}.", deviceId, sensorTypeKey);
                    var clearPayload = JsonSerializer.SerializeToUtf8Bytes(new { DeviceId = deviceId, SensorType = sensorTypeKey, Alert = $"low_threshold{alertTypeSuffix}_cleared", Timestamp = DateTime.UtcNow });

                    // Publish alert for device
                    string alertTopic = string.Format(_nodeConfigs.PublishDevicesAlertTopic, deviceId, $"{sensorTypeKey.ToLower()}_high{alertTypeSuffix}_cleared");
                    await _communicator.PublishAsync(alertTopic, clearPayload, qos: MqttQualityOfServiceLevel.AtLeastOnce);

                    // Publish command for device here
                    string commandTopic = string.Format(_nodeConfigs.PublishDevicesCommandTopic, deviceId, $"{sensorTypeKey.ToLower()}_high{alertTypeSuffix}");
                    await _communicator.PublishAsync(commandTopic, clearPayload, qos: MqttQualityOfServiceLevel.AtLeastOnce); // Use QoS 1 for alerts
                }
            }
        }

        private async Task AggregateData(long deviceId, string sensorTypeKey, double value, SensorProcessingConfig config)
        {
            // Optimization: Skip aggregation if window is zero or negative
            if (config.AggregationWindowSeconds <= 0) return;

            string aggregationKey = $"{deviceId}:{sensorTypeKey}";
            var buffer = _aggregationBuffer.GetOrAdd(aggregationKey, _ => new List<double>());
            DateTime lastTime = _lastAggregationTime.GetOrAdd(aggregationKey, DateTime.UtcNow); // Record time on first message

            // Add current value to buffer (thread-safe via ConcurrentDictionary retrieval)
            // Lock the list briefly for modification
            lock (buffer)
            {
                buffer.Add(value);
            }

            // Check if aggregation window has passed
            if ((DateTime.UtcNow - lastTime).TotalSeconds >= config.AggregationWindowSeconds)
            {
                List<double> valuesToProcess;
                // Lock buffer briefly to swap it out
                lock (buffer)
                {
                    valuesToProcess = new List<double>(buffer); // Copy values
                    buffer.Clear(); // Clear the original buffer
                }

                // Update last aggregation time (outside lock)
                _lastAggregationTime.TryUpdate(aggregationKey, DateTime.UtcNow, lastTime); // Update optimistically

                if (valuesToProcess.Any())
                {
                    // Calculate Aggregates
                    var count = valuesToProcess.Count;
                    var avg = valuesToProcess.Average();
                    var min = valuesToProcess.Min();
                    var max = valuesToProcess.Max();

                    _logger.LogInformation("AGGREGATED ({Window}s) for {Key}: Count={Count}, Avg={Avg}, Min={Min}, Max={Max}",
                        config.AggregationWindowSeconds, aggregationKey, count, avg, min, max);

                    // --- Publish Aggregated Data ---
                    var aggregatedPayload = new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "sensorType", sensorTypeKey },
                        { "timestamp", DateTime.UtcNow },
                        { "aggregationWindowSeconds", config.AggregationWindowSeconds }
                    };

                    if (config.PublishAverage) aggregatedPayload.Add("average", Math.Round(avg, 2)); // Example rounding
                    if (config.PublishMinMax)
                    {
                        aggregatedPayload.Add("minimum", min);
                        aggregatedPayload.Add("maximum", max);
                    }
                    if (config.PublishCount) aggregatedPayload.Add("count", count);


                    if (aggregatedPayload.Count > 4) // Only publish if there's actual aggregate data
                    {
                        string processedTopic = string.Format(_nodeConfigs.PublishGatewayProcessedDataTopic, deviceId, $"{sensorTypeKey.ToLower()}_agg");

                        // Publish data for gateway
                        var payload = JsonSerializer.SerializeToUtf8Bytes(aggregatedPayload);
                        await _communicator.PublishAsync(processedTopic, payload, qos: MqttQualityOfServiceLevel.AtMostOnce);

                        // Optional: Check thresholds on aggregated data as well
                        // await CheckThresholds(deviceId, sensorTypeKey, avg, config, isAggregate: true);
                    }
                }
                else
                {
                    _logger.LogDebug("Aggregation window passed for {Key}, but no data in buffer.", aggregationKey);
                }
            }
        }

        public bool CanProcess(string topic)
        {
            // Use Regex to check if the topic matches the configured device data pattern
            return _deviceDataTopicRegex.IsMatch(topic);
        }

    }


}

