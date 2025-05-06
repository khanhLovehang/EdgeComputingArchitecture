namespace Nodes.Configs
{
    public class SensorProcessingConfig
    {
        public string Unit { get; set; } = string.Empty;
        public double? MinValue { get; set; } // Nullable for optional filtering
        public double? MaxValue { get; set; }
        public double? HighThreshold { get; set; } // Nullable for optional alerting
        public double? LowThreshold { get; set; }
        public int AggregationWindowSeconds { get; set; } = 60; // Default aggregation time
        public bool PublishAverage { get; set; } = true; // Control what aggregates are sent
        public bool PublishMinMax { get; set; } = false;
        public bool PublishCount { get; set; } = false;
    }

    // Main ProcessingSettings class to hold configs per sensor type
    public class ProcessingConfigs : Dictionary<string, SensorProcessingConfig>
    {
        // Allows binding like "ProcessingSettings:Temperature:MinValue"
        // The key will be the sensor type (e.g., "Temperature", "Humidity")
    }
}
