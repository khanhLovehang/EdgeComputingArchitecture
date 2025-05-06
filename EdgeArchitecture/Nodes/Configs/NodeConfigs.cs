namespace Nodes.Configs
{
    public class NodeConfigs
    {
        public string NodeId { get; set; }
        public string SubcribeGatewayCommandTopic { get; set; }
        public string SubcribeDevicesDataTopic { get; set; }
        public string PublishGatewayProcessedDataTopic { get; set; }
        public string PublishDevicesCommandTopic { get; set; }
        public string PublishDevicesAlertTopic { get; set; }
        public int ProcessingParallelism { get; set; }
        public int ChannelCapacity { get; set; }
    }
}
