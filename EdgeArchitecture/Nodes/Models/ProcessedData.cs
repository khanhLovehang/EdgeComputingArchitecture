namespace Nodes.Models
{
    public class ProcessedData
    {
        public string NodeId { get; set; }
        public int OriginalDeviceId { get; set; }
        public DateTime ProcessingTimestamp { get; set; }
        public DateTime ReceivedTimestamp { get; set; }
        public int MeasurementType { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public bool IsCritical { get; set; }
    }
}
