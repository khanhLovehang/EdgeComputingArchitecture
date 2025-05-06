

namespace Shared.Abstractions
{
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string? Reason { get; } // Optional reason for disconnect

        public ConnectionStatusChangedEventArgs(bool isConnected, string? reason = null)
        {
            IsConnected = isConnected;
            Reason = reason;
        }
    }
}
