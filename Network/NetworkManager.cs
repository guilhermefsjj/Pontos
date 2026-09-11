using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Pontos.Models;
using Pontos.Security;

namespace Pontos.Network
{
    /// <summary>
    /// Handles network communication for discovering and communicating with other Pontos instances.
    /// Uses UDP broadcast for peer discovery on local network only.
    /// All messages are validated using HMAC signatures.
    /// </summary>
    public class NetworkManager
    {
        private const int BROADCAST_PORT = 45678;
        private const int MESSAGE_TIMEOUT_MS = 5000;
        private const string BROADCAST_ADDRESS = "255.255.255.255";

        private readonly UserProgress _localProgress;
        private readonly IntegrityValidator _validator;
        private ObservableCollection<NetworkPeer> _peers;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _discoveryTask;
        private UdpClient? _udpClient;
        private bool _isEnabled;

        /// <summary>
        /// Raised when peers are discovered or updated.
        /// </summary>
        public event EventHandler<ObservableCollection<NetworkPeer>>? PeersUpdated;

        /// <summary>
        /// Raised when network error occurs.
        /// </summary>
        public event EventHandler<string>? ErrorOccurred;

        public NetworkManager(UserProgress localProgress)
        {
            _localProgress = localProgress ?? throw new ArgumentNullException(nameof(localProgress));
            _validator = new IntegrityValidator();
            _peers = new ObservableCollection<NetworkPeer>();
            _isEnabled = false;
        }

        /// <summary>
        /// Gets the list of discovered peers.
        /// </summary>
        public ObservableCollection<NetworkPeer> GetPeers()
        {
            return _peers;
        }

        /// <summary>
        /// Enables network mode and starts peer discovery.
        /// </summary>
        public bool EnableNetworkMode()
        {
            if (_isEnabled)
                return true;

            try
            {
                _isEnabled = true;
                _cancellationTokenSource = new CancellationTokenSource();
                _udpClient = new UdpClient { EnableBroadcast = true };
                _udpClient.Client.ReceiveTimeout = MESSAGE_TIMEOUT_MS;

                // Start listening for broadcasts
                _discoveryTask = PeerDiscoveryLoop(_cancellationTokenSource.Token);

                System.Diagnostics.Debug.WriteLine("[INFO] Network mode enabled");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to enable network mode: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
                _isEnabled = false;
                return false;
            }
        }

        /// <summary>
        /// Disables network mode and stops peer discovery.
        /// </summary>
        public void DisableNetworkMode()
        {
            if (!_isEnabled)
                return;

            try
            {
                _isEnabled = false;
                _cancellationTokenSource?.Cancel();
                _udpClient?.Dispose();

                try
                {
                    _discoveryTask?.Wait(2000);
                }
                catch { /* Ignore timeout */ }

                _peers.Clear();
                System.Diagnostics.Debug.WriteLine("[INFO] Network mode disabled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to disable network mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcasts the local machine's status to the network.
        /// </summary>
        private async Task BroadcastLocalStatus()
        {
            if (!_isEnabled || _udpClient == null)
                return;

            try
            {
                var message = new NetworkMessage
                {
                    MessageType = "status_update",
                    SenderMachineId = _localProgress.MachineId,
                    SenderComputerName = Environment.MachineName,
                    Timestamp = DateTime.UtcNow,
                    Points = _localProgress.TotalPoints,
                    Level = _localProgress.CurrentLevel
                };

                // Sign the message
                string messageJson = JsonConvert.SerializeObject(message);
                byte[] key = CryptoHelper.DeriveSecurityKey();
                message.Signature = CryptoHelper.ComputeHmacSha256(messageJson, key);

                // Broadcast
                string signedJson = JsonConvert.SerializeObject(message);
                byte[] data = Encoding.UTF8.GetBytes(signedJson);
                
                await _udpClient.SendAsync(data, data.Length,
                    new IPEndPoint(IPAddress.Parse(BROADCAST_ADDRESS), BROADCAST_PORT));

                System.Diagnostics.Debug.WriteLine(
                    $"[INFO] Broadcasted status: {_localProgress.TotalPoints} points");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARNING] Failed to broadcast status: {ex.Message}");
            }
        }

        /// <summary>
        /// Main loop for peer discovery.
        /// </summary>
        private async Task PeerDiscoveryLoop(CancellationToken cancellationToken)
        {
            try
            {
                var listenEndpoint = new IPEndPoint(IPAddress.Any, BROADCAST_PORT);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Broadcast our status
                        await BroadcastLocalStatus();

                        // Listen for peer broadcasts (non-blocking with timeout)
                        Task<UdpReceiveResult> receiveTask = _udpClient!.ReceiveAsync();
                        Task completedTask = await Task.WhenAny(
                            receiveTask,
                            Task.Delay(2000, cancellationToken));

                        if (completedTask == receiveTask)
                        {
                            var result = receiveTask.Result;
                            ProcessPeerMessage(result.Buffer, result.RemoteEndPoint);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // UDP client was disposed
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[WARNING] Error in peer discovery loop: {ex.Message}");
                    }

                    // Cleanup old peers (not seen for 30 seconds)
                    RemoveStaleePeers();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Peer discovery loop failed: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        /// <summary>
        /// Processes a message received from a peer.
        /// </summary>
        private void ProcessPeerMessage(byte[] messageData, IPEndPoint remoteEndPoint)
        {
            try
            {
                string messageJson = Encoding.UTF8.GetString(messageData);
                var message = JsonConvert.DeserializeObject<NetworkMessage>(messageJson);

                if (message == null)
                    return;

                // Ignore messages from ourselves
                if (message.SenderMachineId == _localProgress.MachineId)
                    return;

                // Validate message signature
                if (!ValidateNetworkMessage(message))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[WARNING] Received message with invalid signature from {message.SenderMachineId}");
                    return;
                }

                // Update or add peer
                var existingPeer = _peers.FirstOrDefault(p => p.MachineId == message.SenderMachineId);
                if (existingPeer != null)
                {
                    existingPeer.Points = message.Points;
                    existingPeer.Level = message.Level;
                    existingPeer.LastSeenAt = DateTime.UtcNow;
                    existingPeer.IpAddress = remoteEndPoint.Address.ToString();
                }
                else
                {
                    var newPeer = new NetworkPeer
                    {
                        MachineId = message.SenderMachineId,
                        ComputerName = message.SenderComputerName,
                        IpAddress = remoteEndPoint.Address.ToString(),
                        Port = BROADCAST_PORT,
                        Points = message.Points,
                        Level = message.Level,
                        LastSeenAt = DateTime.UtcNow,
                        PublicKeyHash = message.Signature
                    };
                    _peers.Add(newPeer);
                }

                PeersUpdated?.Invoke(this, _peers);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARNING] Failed to process peer message: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes peers that haven't been seen for 30 seconds.
        /// </summary>
        private void RemoveStaleePeers()
        {
            var staleePeers = _peers
                .Where(p => DateTime.UtcNow - p.LastSeenAt > TimeSpan.FromSeconds(30))
                .ToList();

            foreach (var peer in staleePeers)
            {
                _peers.Remove(peer);
            }
        }

        /// <summary>
        /// Validates a network message's signature.
        /// </summary>
        private bool ValidateNetworkMessage(NetworkMessage message)
        {
            if (string.IsNullOrEmpty(message.Signature))
                return false;

            try
            {
                // Create a copy without signature for validation
                var messageForValidation = new NetworkMessage
                {
                    MessageType = message.MessageType,
                    SenderMachineId = message.SenderMachineId,
                    SenderComputerName = message.SenderComputerName,
                    Timestamp = message.Timestamp,
                    Points = message.Points,
                    Level = message.Level,
                    Signature = null
                };

                string messageJson = JsonConvert.SerializeObject(messageForValidation);
                byte[] key = CryptoHelper.DeriveSecurityKey();

                return CryptoHelper.VerifyHmacSha256(messageJson, message.Signature, key);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Represents a network message between Pontos instances.
    /// </summary>
    public class NetworkMessage
    {
        public string MessageType { get; set; }
        public string SenderMachineId { get; set; }
        public string SenderComputerName { get; set; }
        public DateTime Timestamp { get; set; }
        public int Points { get; set; }
        public int Level { get; set; }
        public string? Signature { get; set; }
    }
}
