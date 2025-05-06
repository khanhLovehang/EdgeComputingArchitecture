using System.ComponentModel.DataAnnotations;

namespace Gateway.Requests
{
    public class GatewayDataRequest
    {
        public string? DeviceId { get; set; }
        public string? Topic { get; set; } // Topic the data came from
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // The actual payload - assuming it's JSON sent as a string
        public string? PayloadJson { get; set; }
    }
}
