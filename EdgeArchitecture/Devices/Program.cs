using Devices.Background;
using Devices.Configs;
using Devices.Services;
using Devices.Simulator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Extensions;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
         .ConfigureAppConfiguration((context, config) =>
         {
             // Ensure appsettings.json is included
             config.SetBasePath(Directory.GetCurrentDirectory());
             config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
             // Add environment variables, command line args etc. if needed
         })
         .ConfigureServices((hostContext, services) =>
         {
             services.Configure<DeviceConfigs>(hostContext.Configuration.GetSection("DeviceConfigs"));
             // Register your services
             services.AddHttpClient<IDeviceServices, DeviceServices>((serviceProvider, client) =>
             {
                 // Get base URL from configuration
                 var configs = serviceProvider.GetRequiredService<IOptions<DeviceConfigs>>().Value;
                 if (!string.IsNullOrWhiteSpace(configs.DashboardApiBaseUrl))
                 {
                     client.BaseAddress = new Uri(configs.DashboardApiBaseUrl);
                 }
                 else
                 {
                     // Handle missing configuration - Log or throw
                     var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                     logger.LogError("DashboardApiBaseUrl is not configured in appsettings.json");
                     // Consider throwing an exception if this is critical
                     // throw new InvalidOperationException("DashboardApiBaseUrl is required.");
                 }
                 // Add any default headers if needed, e.g., API key
                 // client.DefaultRequestHeaders.Add("X-Api-Key", "your-key");
             });

             services.AddTransient<DeviceSimulator>();

             services.AddMqttCommunicator(hostContext.Configuration, "MqttConfigs");

             services.AddHostedService<DevicesWorkerService>(); // Example worker
         })
         .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();
        })
         .Build();

        Console.WriteLine("Edge Device Simulator starting. Press Ctrl+C to exit.");
        await host.RunAsync(); // Runs the host and blocks until shutdown (Ctrl+C)
    }
}