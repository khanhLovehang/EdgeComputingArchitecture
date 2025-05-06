namespace Gateway.Models
{
    public class QueuedMessage
    {
        public string Topic { get; }
        public byte[] Payload { get; }
        public DateTime ReceivedTimestamp { get; }

        public QueuedMessage(string topic, byte[] payload)
        {
            Topic = topic;
            Payload = payload; // Raw payload
            ReceivedTimestamp = DateTime.UtcNow;
        }
    }
}
