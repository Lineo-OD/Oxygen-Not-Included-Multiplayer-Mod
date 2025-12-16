using System;
using System.Collections.Generic;
using Steamworks;
using LiteNetLib.Utils;
using OniMultiplayer.UI;

namespace OniMultiplayer.Network
{
    /// <summary>
    /// Manages Steam P2P networking for actual game data transfer.
    /// Uses Steam's relay servers - no port forwarding needed!
    /// Max 3 players supported.
    /// </summary>
    public class SteamP2PManager
    {
        public static SteamP2PManager Instance { get; private set; }

        /// <summary>
        /// Maximum number of players allowed (host + clients).
        /// </summary>
        public const int MaxPlayers = 3;

        // Connection state
        public bool IsHost { get; private set; }
        public bool IsConnected { get; private set; }
        public CSteamID HostSteamId { get; private set; }
        public int LocalPlayerId { get; private set; } = -1;

        // Connected peers (host tracks clients, clients track host)
        private readonly Dictionary<CSteamID, int> _peerToPlayerId = new Dictionary<CSteamID, int>();
        private readonly Dictionary<int, CSteamID> _playerIdToPeer = new Dictionary<int, CSteamID>();
        private readonly Dictionary<int, string> _playerNames = new Dictionary<int, string>();
        private int _nextPlayerId = 1; // 0 is always host
        
        // Track pending/processed connection requests to prevent duplicates
        private readonly HashSet<CSteamID> _pendingConnections = new HashSet<CSteamID>();
        
        // Client: track if we've already sent a connection request
        private bool _connectionRequestSent = false;

        // Steam callbacks
        private Callback<P2PSessionRequest_t> _p2pSessionRequestCallback;
        private Callback<P2PSessionConnectFail_t> _p2pSessionConnectFailCallback;

        // Channel IDs
        private const int ReliableChannel = 0;
        private const int UnreliableChannel = 1;

        // Events (use delegates for proper += support)
        public delegate void PeerHandler(CSteamID steamId, int playerId);
        public delegate void VoidHandler();
        
        public event PeerHandler OnPeerConnected;
        public event PeerHandler OnPeerDisconnected;
        public event VoidHandler OnConnectedToHost;
        public event VoidHandler OnDisconnectedFromHost;

        // Packet buffer
        private readonly byte[] _receiveBuffer = new byte[4096];

        public static void Initialize()
        {
            if (Instance != null) return;

            if (!SteamManager.Initialized)
            {
                OniMultiplayerMod.LogError("Steam not initialized!");
                return;
            }

            Instance = new SteamP2PManager();
            Instance.RegisterCallbacks();
            OniMultiplayerMod.Log("SteamP2PManager initialized");
        }

        private void RegisterCallbacks()
        {
            _p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
            _p2pSessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);
        }

        /// <summary>
        /// Start hosting - called when lobby owner starts the game.
        /// </summary>
        public void StartHost()
        {
            IsHost = true;
            IsConnected = true;
            LocalPlayerId = 0;
            HostSteamId = SteamUser.GetSteamID();

            _peerToPlayerId.Clear();
            _playerIdToPeer.Clear();
            _nextPlayerId = 1;

            OniMultiplayerMod.Log("Started as host");
        }

        /// <summary>
        /// Connect to host - called when joining a game.
        /// </summary>
        public void ConnectToHost(CSteamID hostId)
        {
            OniMultiplayerMod.Log($"[P2P] ConnectToHost called. hostId={hostId}, _connectionRequestSent={_connectionRequestSent}, current HostSteamId={HostSteamId}");
            
            // Prevent duplicate connection requests
            if (_connectionRequestSent && HostSteamId == hostId)
            {
                OniMultiplayerMod.Log($"[P2P] Connection request already sent to host {hostId}, ignoring duplicate");
                return;
            }
            
            IsHost = false;
            HostSteamId = hostId;
            _connectionRequestSent = true;

            // Send a connection request packet
            var writer = new NetDataWriter();
            writer.Put((byte)255); // Special: connection request
            writer.Put(SteamFriends.GetPersonaName());
            
            OniMultiplayerMod.Log($"[P2P] Sending connection request (packet 255) to host {hostId}, payload size={writer.Length}");
            SendToSteamId(hostId, writer.Data, writer.Length, EP2PSend.k_EP2PSendReliable);
            
            OniMultiplayerMod.Log($"[P2P] Connection request sent! IsHost={IsHost}, HostSteamId={HostSteamId}, _connectionRequestSent={_connectionRequestSent}");
        }

        /// <summary>
        /// Stop all P2P connections.
        /// </summary>
        public void Stop()
        {
            // Close all P2P sessions
            foreach (var peer in _peerToPlayerId.Keys)
            {
                SteamNetworking.CloseP2PSessionWithUser(peer);
            }

            _peerToPlayerId.Clear();
            _playerIdToPeer.Clear();
            _playerNames.Clear();
            _pendingConnections.Clear();
            _connectionRequestSent = false;
            IsHost = false;
            IsConnected = false;
            LocalPlayerId = -1;
            HostSteamId = CSteamID.Nil;

            OniMultiplayerMod.Log("P2P stopped");
        }

        /// <summary>
        /// Send packet to host (client only).
        /// </summary>
        public void SendToHost(INetSerializable packet, bool reliable = true)
        {
            if (IsHost || !IsConnected) return;

            var writer = new NetDataWriter();
            writer.Put(PacketRegistry.GetPacketId(packet.GetType()));
            packet.Serialize(writer);

            var sendType = reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliableNoDelay;
            SendToSteamId(HostSteamId, writer.Data, writer.Length, sendType);
        }

        /// <summary>
        /// Send packet to a specific client (host only).
        /// </summary>
        public void SendToClient(int playerId, INetSerializable packet, bool reliable = true)
        {
            if (!IsHost || !_playerIdToPeer.TryGetValue(playerId, out var steamId)) return;

            var writer = new NetDataWriter();
            writer.Put(PacketRegistry.GetPacketId(packet.GetType()));
            packet.Serialize(writer);

            var sendType = reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliableNoDelay;
            SendToSteamId(steamId, writer.Data, writer.Length, sendType);
        }

        /// <summary>
        /// Broadcast packet to all connected clients (host only).
        /// </summary>
        public void BroadcastToClients(INetSerializable packet, bool reliable = true)
        {
            if (!IsHost)
            {
                OniMultiplayerMod.Log($"[P2P] BroadcastToClients called but not host, ignoring");
                return;
            }

            byte packetId = PacketRegistry.GetPacketId(packet.GetType());
            OniMultiplayerMod.Log($"[P2P] BroadcastToClients: {packet.GetType().Name} (packetId={packetId}) to {_peerToPlayerId.Count} clients");

            var writer = new NetDataWriter();
            writer.Put(packetId);
            packet.Serialize(writer);

            var sendType = reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliableNoDelay;

            int sentCount = 0;
            foreach (var steamId in _peerToPlayerId.Keys)
            {
                OniMultiplayerMod.Log($"[P2P] Sending to peer {steamId}...");
                SendToSteamId(steamId, writer.Data, writer.Length, sendType);
                sentCount++;
            }
            OniMultiplayerMod.Log($"[P2P] Broadcast complete: sent to {sentCount} peers");
        }

        /// <summary>
        /// Poll for incoming packets - call every frame.
        /// </summary>
        public void Update()
        {
            // Process packets if:
            // - We're the host (always process)
            // - We're connected as client
            // - We're attempting to connect (waiting for welcome packet)
            bool hasValidHost = HostSteamId.IsValid() && HostSteamId != CSteamID.Nil;
            bool shouldProcess = IsHost || IsConnected || hasValidHost;
            
            if (!shouldProcess) return;

            // Check both channels
            ProcessIncomingPackets(ReliableChannel);
            ProcessIncomingPackets(UnreliableChannel);
        }
        
        // Debug: log state periodically
        private float _lastStateLogTime = 0f;
        public void LogState()
        {
            if (UnityEngine.Time.time - _lastStateLogTime > 5f)
            {
                _lastStateLogTime = UnityEngine.Time.time;
                OniMultiplayerMod.Log($"[P2P State] IsHost={IsHost}, IsConnected={IsConnected}, LocalPlayerId={LocalPlayerId}, HostSteamId={HostSteamId}, Peers={_peerToPlayerId.Count}, ConnectionRequestSent={_connectionRequestSent}");
            }
        }

        private void ProcessIncomingPackets(int channel)
        {
            uint msgSize;
            while (SteamNetworking.IsP2PPacketAvailable(out msgSize, channel))
            {
                if (msgSize > _receiveBuffer.Length)
                {
                    OniMultiplayerMod.LogWarning($"Packet too large: {msgSize} bytes");
                    continue;
                }

                CSteamID remoteSteamId;
                if (SteamNetworking.ReadP2PPacket(_receiveBuffer, msgSize, out uint bytesRead, out remoteSteamId, channel))
                {
                    OniMultiplayerMod.Log($"[P2P] Received packet: {bytesRead} bytes from {remoteSteamId} on channel {channel}, packetId={_receiveBuffer[0]}");
                    HandlePacket(remoteSteamId, _receiveBuffer, (int)bytesRead);
                }
            }
        }

        private void HandlePacket(CSteamID sender, byte[] data, int length)
        {
            if (length < 1) return;

            byte packetId = data[0];

            // Special packet: connection request
            if (packetId == 255 && IsHost)
            {
                HandleConnectionRequest(sender, data, length);
                return;
            }

            // Special packet: welcome (client receives player ID)
            if (packetId == 254 && !IsHost)
            {
                HandleWelcome(data, length);
                return;
            }

            // Regular game packet
            var reader = new NetDataReader(data, 0, length);
            byte id = reader.GetByte();
            var packet = PacketRegistry.CreatePacket(id);

            if (packet != null)
            {
                packet.Deserialize(reader);

                // Determine player ID for this sender
                int playerId = IsHost ? GetPlayerIdFromSteamId(sender) : 0;
                
                PacketHandler.HandlePacket(playerId, packet, IsHost);
            }
        }

        private void HandleConnectionRequest(CSteamID sender, byte[] data, int length)
        {
            OniMultiplayerMod.Log($"[P2P Host] HandleConnectionRequest from {sender}, data length={length}");
            
            var reader = new NetDataReader(data, 1, length - 1); // Skip packet ID
            string playerName = reader.GetString();
            
            OniMultiplayerMod.Log($"[P2P Host] Connection request from player '{playerName}' (Steam: {sender})");

            // Check if this player is already connected (duplicate connection request)
            if (_peerToPlayerId.TryGetValue(sender, out int existingPlayerId))
            {
                OniMultiplayerMod.Log($"[P2P Host] Duplicate connection request from {playerName} (Steam: {sender}, existing ID: {existingPlayerId}) - resending welcome");
                
                // Just resend the welcome packet with their existing ID
                var resendWriter = new NetDataWriter();
                resendWriter.Put((byte)254); // Welcome packet
                resendWriter.Put(existingPlayerId);
                resendWriter.Put(SteamFriends.GetPersonaName()); // Host name
                OniMultiplayerMod.Log($"[P2P Host] Resending welcome packet (254) to {sender}");
                SendToSteamId(sender, resendWriter.Data, resendWriter.Length, EP2PSend.k_EP2PSendReliable);
                return;
            }

            // Check for reconnection
            if (Systems.ReconnectionManager.Instance?.CanReconnect(sender, out int previousPlayerId, out var previousDupes) == true)
            {
                // This is a reconnecting player - restore their previous ID
                int playerId = previousPlayerId;
                _peerToPlayerId[sender] = playerId;
                _playerIdToPeer[playerId] = sender;
                _playerNames[playerId] = playerName;

                OniMultiplayerMod.Log($"Player RECONNECTED: {playerName} (restored ID: {playerId}, Steam: {sender})");

                // Send welcome packet with their original ID
                var writer = new NetDataWriter();
                writer.Put((byte)254); // Welcome packet
                writer.Put(playerId);
                writer.Put(SteamFriends.GetPersonaName()); // Host name
                SendToSteamId(sender, writer.Data, writer.Length, EP2PSend.k_EP2PSendReliable);

                OnPeerConnected?.Invoke(sender, playerId);
                
                // Restore dupe ownership
                Systems.ReconnectionManager.Instance.OnPlayerReconnected(sender, playerId);
                return;
            }

            // Check if we've reached max players
            int currentPlayerCount = _peerToPlayerId.Count + 1; // +1 for host
            if (currentPlayerCount >= MaxPlayers)
            {
                OniMultiplayerMod.LogWarning($"Connection rejected from {playerName}: Server full ({currentPlayerCount}/{MaxPlayers})");
                // Send rejection packet
                var rejectWriter = new NetDataWriter();
                rejectWriter.Put((byte)253); // Rejection packet
                rejectWriter.Put("Server is full (max 3 players)");
                SendToSteamId(sender, rejectWriter.Data, rejectWriter.Length, EP2PSend.k_EP2PSendReliable);
                SteamNetworking.CloseP2PSessionWithUser(sender);
                return;
            }

            // Assign new player ID
            int newPlayerId = _nextPlayerId++;
            _peerToPlayerId[sender] = newPlayerId;
            _playerIdToPeer[newPlayerId] = sender;
            _playerNames[newPlayerId] = playerName;

            OniMultiplayerMod.Log($"[P2P Host] *** NEW PLAYER CONNECTED! *** {playerName} (ID: {newPlayerId}, Steam: {sender}) [{currentPlayerCount + 1}/{MaxPlayers}]");

            // Send welcome packet with assigned ID
            var writer2 = new NetDataWriter();
            writer2.Put((byte)254); // Welcome packet
            writer2.Put(newPlayerId);
            writer2.Put(SteamFriends.GetPersonaName()); // Host name
            OniMultiplayerMod.Log($"[P2P Host] Sending welcome packet (254) to {sender} with playerId={newPlayerId}");
            SendToSteamId(sender, writer2.Data, writer2.Length, EP2PSend.k_EP2PSendReliable);
            OniMultiplayerMod.Log($"[P2P Host] Welcome packet sent to {sender}!");

            OnPeerConnected?.Invoke(sender, newPlayerId);
            
            // Notify host that a player connected
            UI.MultiplayerNotification.ShowSuccess($"Player '{playerName}' connected! (ID: {newPlayerId})");
        }

        private void HandleWelcome(byte[] data, int length)
        {
            OniMultiplayerMod.Log($"[P2P] HandleWelcome called! data length={length}, IsConnected={IsConnected}, LocalPlayerId={LocalPlayerId}");
            
            var reader = new NetDataReader(data, 1, length - 1);
            int assignedPlayerId = reader.GetInt();
            string hostName = reader.GetString();

            OniMultiplayerMod.Log($"[P2P] Welcome packet contents: assignedPlayerId={assignedPlayerId}, hostName={hostName}");

            // Prevent duplicate welcome handling
            if (IsConnected && LocalPlayerId == assignedPlayerId)
            {
                OniMultiplayerMod.Log($"[P2P] Duplicate welcome packet received (already connected as Player {LocalPlayerId}), ignoring");
                return;
            }

            LocalPlayerId = assignedPlayerId;
            IsConnected = true;
            OniMultiplayerMod.Log($"[P2P] *** CLIENT CONNECTED! *** Host={hostName}, PlayerID={LocalPlayerId}, IsConnected={IsConnected}");
            
            // Notify client they connected
            UI.MultiplayerNotification.ShowSuccess($"Connected to host '{hostName}'! You are Player {LocalPlayerId}");
            
            OnConnectedToHost?.Invoke();
        }

        private void SendToSteamId(CSteamID target, byte[] data, int length, EP2PSend sendType)
        {
            int channel = (sendType == EP2PSend.k_EP2PSendReliable) ? ReliableChannel : UnreliableChannel;
            bool result = SteamNetworking.SendP2PPacket(target, data, (uint)length, sendType, channel);
            OniMultiplayerMod.Log($"[P2P] SendP2PPacket to {target}: {length} bytes, channel={channel}, sendType={sendType}, result={result}");
        }

        private int GetPlayerIdFromSteamId(CSteamID steamId)
        {
            return _peerToPlayerId.TryGetValue(steamId, out int id) ? id : -1;
        }

        public CSteamID GetSteamIdFromPlayerId(int playerId)
        {
            return _playerIdToPeer.TryGetValue(playerId, out var steamId) ? steamId : CSteamID.Nil;
        }

        public IEnumerable<int> GetAllPlayerIds()
        {
            yield return 0; // Host
            foreach (var id in _peerToPlayerId.Values)
            {
                yield return id;
            }
        }

        /// <summary>
        /// Get the current number of connected players (including host).
        /// </summary>
        public int GetPlayerCount()
        {
            return _peerToPlayerId.Count + 1; // +1 for host
        }

        /// <summary>
        /// Check if the server can accept more players.
        /// </summary>
        public bool CanAcceptMorePlayers()
        {
            return GetPlayerCount() < MaxPlayers;
        }

        /// <summary>
        /// Get player name by ID.
        /// </summary>
        public string GetPlayerName(int playerId)
        {
            if (playerId == 0) return SteamFriends.GetPersonaName(); // Host
            return _playerNames.TryGetValue(playerId, out var name) ? name : $"Player {playerId}";
        }

        #region Steam Callbacks

        private void OnP2PSessionRequest(P2PSessionRequest_t result)
        {
            CSteamID remoteSteamId = result.m_steamIDRemote;
            OniMultiplayerMod.Log($"[P2P] OnP2PSessionRequest from {remoteSteamId}, IsHost={IsHost}, HostSteamId={HostSteamId}");

            if (IsHost)
            {
                // Host accepts all connections from lobby members
                SteamNetworking.AcceptP2PSessionWithUser(remoteSteamId);
                OniMultiplayerMod.Log($"[P2P] Host accepted P2P session from {remoteSteamId}");
            }
            else
            {
                // Client only accepts from host
                if (remoteSteamId == HostSteamId)
                {
                    SteamNetworking.AcceptP2PSessionWithUser(remoteSteamId);
                    OniMultiplayerMod.Log($"[P2P] Client accepted P2P session from host {remoteSteamId}");
                }
                else
                {
                    OniMultiplayerMod.Log($"[P2P] Client REJECTED P2P session from {remoteSteamId} (not host)");
                }
            }
        }

        private void OnP2PSessionConnectFail(P2PSessionConnectFail_t result)
        {
            CSteamID remoteSteamId = result.m_steamIDRemote;
            EP2PSessionError error = (EP2PSessionError)result.m_eP2PSessionError;

            OniMultiplayerMod.LogError($"P2P connection failed with {remoteSteamId}: {error}");

            if (IsHost && _peerToPlayerId.TryGetValue(remoteSteamId, out int playerId))
            {
                _peerToPlayerId.Remove(remoteSteamId);
                _playerIdToPeer.Remove(playerId);
                OnPeerDisconnected?.Invoke(remoteSteamId, playerId);
                
                // Notify host that a player disconnected
                string playerName = SteamFriends.GetFriendPersonaName(remoteSteamId);
                UI.MultiplayerNotification.ShowWarning($"Player '{playerName}' disconnected: {error}");
            }
            else if (!IsHost && remoteSteamId == HostSteamId)
            {
                IsConnected = false;
                OnDisconnectedFromHost?.Invoke();
                
                // Notify client they lost connection
                UI.MultiplayerNotification.ShowError($"Lost connection to host! Error: {error}");
            }
        }

        #endregion
    }
}