using System.ComponentModel.DataAnnotations;

namespace Server.Models
{
    public class GatewayDataRequest
    {
        // Add properties expected from the Gateway
        // Example properties:
        [Required]
        public string? DeviceId { get; set; }
        public string? Topic { get; set; } // Topic the data came from
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // The actual payload - assuming it's JSON sent as a string
        // Or define specific properties if the structure is known
        [Required]
        public string? PayloadJson { get; set; }
    }
}
