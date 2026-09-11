using System;

namespace Pontos.Models
{
    /// <summary>
    /// Represents another computer on the network running the Pontos application.
    /// </summary>
    public class NetworkPeer
    {
        /// <summary>
        /// Unique machine identifier of the peer.
        /// </summary>
        public string MachineId { get; set; }

        /// <summary>
        /// Human-readable computer name.
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// IP address of the peer on the local network.
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// UDP port used for communication.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Current points of the peer (validated).
        /// </summary>
        public int Points { get; set; }

        /// <summary>
        /// Current level of the peer.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Last time this peer was seen online.
        /// </summary>
        public DateTime LastSeenAt { get; set; }

        /// <summary>
        /// Public key for validating messages from this peer.
        /// </summary>
        public string? PublicKeyHash { get; set; }

        public NetworkPeer()
        {
            LastSeenAt = DateTime.UtcNow;
        }
    }
}
