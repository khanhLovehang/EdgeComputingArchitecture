namespace Gateway.Configs
{
    public class GatewayConfigs
    {
        public string GatewayId { get; set; } = "gateway-instance-001";
        public string ServerBaseUrl { get; set; } = "https://localhost:7189/api/ingest";
        public string ServerApiKey { get; set; }
        public string SubcribeNodesDataProcessedTopic { get; set; } = "nodes/+/+/data/processed";
        public int ProcessingParallelism { get; set; } = 4;
        public int UpstreamChannelCapacity { get; set; } = 5000;
    }
}
