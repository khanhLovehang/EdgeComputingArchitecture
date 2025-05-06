using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nodes.Models
{
    public class QueuedMessage
    {
        public string Topic { get; }
        public byte[] Payload { get; } // Keep as byte[] until processed
        public DateTime ReceivedTimestamp { get; }

        public QueuedMessage(string topic, byte[] payload)
        {
            Topic = topic;
            Payload = payload; // Consider if cloning is needed depending on MQTT lib buffer reuse
            ReceivedTimestamp = DateTime.UtcNow;
        }
    }
}
