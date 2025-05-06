using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devices.Models
{
    public class AlertData
    {
        public int DeviceId { get; set; }
        public string SensorType { get; set; }
        public string Alert { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
