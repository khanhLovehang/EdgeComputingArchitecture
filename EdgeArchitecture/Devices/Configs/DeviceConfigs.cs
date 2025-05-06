using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devices.Configs
{
    public class DeviceConfigs
    {
        public string PublishNodesRawDataTopic { get; set; } = "devices/{0}/data";
        public string SubcribeNodesCommandTopic { get; set; } = "nodes/command/devices/+/+";
        public string SubcribeNodesAlertTopic { get; set; } = "nodes/alert/devices/+/+";
        public string DashboardApiBaseUrl { get; set; } = string.Empty;
        public string DeviceApiEndpoint { get; set; } = string.Empty;
        public int PollingIntervalSeconds { get; set; } = 30;
        public int SimulationIntervalSeconds { get; set; } = 5;
        public int TimeSensorPing { get; set; } = 60;
    }
}
