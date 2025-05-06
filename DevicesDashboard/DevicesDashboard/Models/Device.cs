using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevicesDashboard.Models
{
    public class Device
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        [DefaultValue(false)]
        public bool IsDeleted { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public int SensorType { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? LastPing { get; set; } = null;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; } = null;
        public DateTime? DeletedDate { get; set; } = null;
    }
}
