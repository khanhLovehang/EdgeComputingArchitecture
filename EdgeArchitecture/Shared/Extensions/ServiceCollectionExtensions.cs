using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Communication;
using Shared.Configs;

namespace Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds MQTT communication services to the specified IServiceCollection.
        /// </summary>
        /// <param name="services">The IServiceCollection to add the services to.</param>
        /// <param name="configureOptions">An action to configure the MqttConfigs.</param>
        /// <returns>The original IServiceCollection.</returns>
        public static IServiceCollection AddMqttCommunicator(
            this IServiceCollection services,
            Action<MqttConfigs> configureOptions)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configureOptions == null) throw new ArgumentNullException(nameof(configureOptions));

            // Configure MqttConfigs using the standard Options pattern
            services.Configure(configureOptions);

            // Register the MqttCommunicator implementation as a Singleton
            // Singleton is generally appropriate for a client managing a persistent connection
            services.TryAddSingleton<ICommunicator, MqttCommunicator>();

            // Ensure necessary framework services are available (usually added by Host.CreateDefaultBuilder, but good practice)
            services.AddOptions();
            services.AddLogging();

            return services;
        }

        /// <summary>
        /// Adds MQTT communication services using configuration section.
        /// </summary>
        /// <param name="services">The IServiceCollection to add the services to.</param>
        /// <param name="configuration">The IConfiguration instance.</param>
        /// <param name="configSectionName">The name of the configuration section for MqttConfigs (default: "Mqtt").</param>
        /// <returns>The original IServiceCollection.</returns>
        public static IServiceCollection AddMqttCommunicator(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSectionName = "MqttConfigs") // Standard config section name
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // Configure MqttConfigs from the specified configuration section
            services.Configure<MqttConfigs>(configuration.GetSection(configSectionName));

            // Register the MqttCommunicator implementation as Singleton
            services.TryAddSingleton<ICommunicator, MqttCommunicator>();

            services.AddOptions();
            services.AddLogging();

            return services;
        }

    }
}
