using Devices.Models;

namespace Devices.Services
{
    public interface IDeviceServices
    {
        Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default);
    }
}
