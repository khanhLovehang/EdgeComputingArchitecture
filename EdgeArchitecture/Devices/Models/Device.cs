using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devices.Models
{
    public class Device
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int SensorType { get; set; }
        public bool IsActive { get; set; }
    }
}
