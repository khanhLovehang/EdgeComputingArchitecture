using System.ComponentModel.DataAnnotations;

namespace Server.Models
{
    public class DataRecord
    {
        public long Id { get; set; } // Primary Key

        [Required]
        [MaxLength(100)]
        public string DeviceId { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Topic { get; set; }

        public DateTime ReceivedTimestamp { get; set; } // When server received it
        public DateTime? OriginalTimestamp { get; set; } // Timestamp from the device/node if available

        [Required]
        public string ProcessedPayload { get; set; } = string.Empty; // Store processed data

        public DateTime ProcessingCompletedTimestamp { get; set; } // When processing finished
    }
}
