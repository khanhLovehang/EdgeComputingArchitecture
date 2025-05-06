using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Configs
{
    public class MqttConfigs
    {
        public string ClientId { get; set; } = Guid.NewGuid().ToString();
        public string BrokerAddress { get; set; } = "localhost";
        public int Port { get; set; } = 1883; // Default MQTT port

        // --- Credentials ---
        public string? Username { get; set; }
        public string? Password { get; set; } // Consider secure handling (e.g., SecureString, KeyVault)

        // --- TLS/Security ---
        public bool UseTls { get; set; } = false;
        public bool TlsAllowUntrustedCertificates { get; set; } = false;
        public bool TlsIgnoreCertificateRevocationErrors { get; set; } = false;
        public bool TlsIgnoreCertificateChainErrors { get; set; } = false;
        // Add paths to client certificates/keys if using mutual TLS (mTLS)
        // public string? ClientCertificatePath { get; set; }
        // public string? ClientCertificatePassword { get; set; }

        // --- Connection Behavior ---
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan KeepAlivePeriod { get; set; } = TimeSpan.FromSeconds(20);

        // --- Initial Subscriptions ---
        /// <summary>
        /// List of topics to subscribe to automatically on connection.
        /// Key: Topic Filter, Value: QoS Level
        /// </summary>
        public Dictionary<string, MqttQualityOfServiceLevel> InitialSubscriptions { get; set; } = new Dictionary<string, MqttQualityOfServiceLevel>();

        // --- LWT (Last Will and Testament) --- Optional
        public bool UseLwt { get; set; } = false;
        public string? LwtTopic { get; set; }
        public byte[]? LwtPayload { get; set; }
        public MqttQualityOfServiceLevel LwtQos { get; set; } = MqttQualityOfServiceLevel.AtLeastOnce;
        public bool LwtRetain { get; set; } = false;

        // Add other relevant MQTTnet options as needed
    }
}
