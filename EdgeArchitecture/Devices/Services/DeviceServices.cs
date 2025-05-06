using Devices.Configs;
using Devices.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Devices.Services
{
    public class DeviceServices : IDeviceServices
    {
        private readonly HttpClient _httpClient;
        private readonly DeviceConfigs _deviceConfigs;
        private readonly ILogger<DeviceServices> _logger;
        private readonly string _deviceApiUrl;

        public DeviceServices(HttpClient httpClient, IOptions<DeviceConfigs> deviceConfigs, ILogger<DeviceServices> logger)
        {
            _httpClient = httpClient;
            _deviceConfigs = deviceConfigs.Value;
            _logger = logger;

            // Construct the full API URL once
            var baseUri = new Uri(_deviceConfigs.DashboardApiBaseUrl);
            _deviceApiUrl = new Uri(baseUri, _deviceConfigs.DeviceApiEndpoint).ToString();

            _logger.LogInformation("Device API Endpoint: {Url}", _deviceApiUrl);
        }

        public async Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Fetching device list from {Url}", _deviceApiUrl);

                var response = await _httpClient.GetAsync(_deviceApiUrl, cancellationToken);
                response.EnsureSuccessStatusCode(); // Throw if HTTP request failed

                var devices = await response.Content.ReadFromJsonAsync<List<Device>>(cancellationToken: cancellationToken);
                _logger.LogDebug("Successfully fetched {Count} devices", devices?.Count ?? 0);
                return devices ?? new List<Device>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error fetching devices from API at {Url}", _deviceApiUrl);
                return new List<Device>(); // Return empty list on error
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching devices.");
                return new List<Device>();
            }
        }
    }
}
