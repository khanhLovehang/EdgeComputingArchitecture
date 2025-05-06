using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Abstractions
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; }
        public byte[] Payload { get; }
        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; }
        public bool Retain { get; }
        // Add timestamp, etc. if needed

        public MessageReceivedEventArgs(string topic, byte[] payload, MqttQualityOfServiceLevel qos, bool retain)
        {
            Topic = topic;
            Payload = payload; // Consider cloning if the buffer might be reused
            QualityOfServiceLevel = qos;
            Retain = retain;
        }
    }
}
