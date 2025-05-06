using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devices.Data
{
    public class GenerateData
    {

        public double GenSensorData(int sensorType)
        {
            var value = 0;

            switch (sensorType)
            {
                case 1:
                    return TemperatureData();
                case 2:
                    return HumidityData();
                case 3:
                    return SoilmoistureData();
                default:
                    break;
            }

            return value;
        }

        private double TemperatureData()
        {
            var value = 35;

            return value;
        }

        private double HumidityData()
        {
            var value = 35;

            return value;
        }

        private double SoilmoistureData()
        {
            var value = 35;

            return value;
        }
    }
}
