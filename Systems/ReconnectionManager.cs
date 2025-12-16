using System.Collections.Generic;
using Steamworks;
using OniMultiplayer.Network;
using OniMultiplayer.UI;

namespace OniMultiplayer.Systems
{
    /// <summary>
    /// Manages player reconnection in multiplayer sessions.
    /// 
    /// When a player disconnects:
    /// - Their state is preserved for a grace period
    /// - Their dupe is "paused" (won't do tasks)
    /// - If they reconnect, they get their dupe back
    /// - After timeout, their slot is freed
    /// </summary>
    public class ReconnectionManager
    {
        public static ReconnectionManager Instance { get; private set; }

        // Grace period before a disconnected player's slot is freed
        private const float ReconnectionGracePeriod = 120f; // 2 minutes

        // Disconnected player data
        private class DisconnectedPlayer
        {
            public CSteamID SteamId;
            public int PlayerId;
            public string PlayerName;
            public List<string> OwnedDupeNames;
            public float DisconnectTime;
        }

        private readonly Dictionary<ulong, DisconnectedPlayer> _disconnectedPlayers = 
            new Dictionary<ulong, DisconnectedPlayer>();

        public static void Initialize()
        {
            Instance = new ReconnectionManager();
            
            // Subscribe to P2P events
            if (SteamP2PManager.Instance != null)
            {
                SteamP2PManager.Instance.OnPeerDisconnected += Instance.OnPlayerDisconnected;
            }
            
            OniMultiplayerMod.Log("[Reconnection] Manager initialized");
        }

        /// <summary>
        /// Called when a player disconnects.
        /// </summary>
        private void OnPlayerDisconnected(CSteamID steamId, int playerId)
        {
            if (SteamP2PManager.Instance?.IsHost != true) return;

            string playerName = SteamFriends.GetFriendPersonaName(steamId);
            
            // Get their owned dupes
            var ownedDupes = DupeOwnership.Instance?.GetOwnedDupeNames(playerId) ?? new List<string>();
            
            // Store disconnect info
            _disconnectedPlayers[steamId.m_SteamID] = new DisconnectedPlayer
            {
                SteamId = steamId,
                PlayerId = playerId,
                PlayerName = playerName,
                OwnedDupeNames = new List<string>(ownedDupes),
                DisconnectTime = UnityEngine.Time.time
            };

            OniMultiplayerMod.Log($"[Reconnection] Player '{playerName}' (ID: {playerId}) disconnected. " +
                                  $"Preserving {ownedDupes.Count} dupe(s) for reconnection.");

            // Notify other players
            MultiplayerNotification.ShowWarning(
                $"Player '{playerName}' disconnected.\n" +
                $"They have {ReconnectionGracePeriod}s to reconnect."
            );
        }

        /// <summary>
        /// Check if a Steam ID can reconnect (was recently disconnected).
        /// </summary>
        public bool CanReconnect(CSteamID steamId, out int previousPlayerId, out List<string> previousDupes)
        {
            previousPlayerId = -1;
            previousDupes = null;

            if (!_disconnectedPlayers.TryGetValue(steamId.m_SteamID, out var data))
            {
                return false;
            }

            float elapsed = UnityEngine.Time.time - data.DisconnectTime;
            if (elapsed > ReconnectionGracePeriod)
            {
                // Grace period expired
                _disconnectedPlayers.Remove(steamId.m_SteamID);
                return false;
            }

            previousPlayerId = data.PlayerId;
            previousDupes = data.OwnedDupeNames;
            return true;
        }

        /// <summary>
        /// Called when a player successfully reconnects.
        /// </summary>
        public void OnPlayerReconnected(CSteamID steamId, int playerId)
        {
            if (!_disconnectedPlayers.TryGetValue(steamId.m_SteamID, out var data))
            {
                return;
            }

            string playerName = data.PlayerName;
            
            // Restore their dupe ownership
            if (data.OwnedDupeNames != null && data.OwnedDupeNames.Count > 0)
            {
                foreach (var dupeName in data.OwnedDupeNames)
                {
                    DupeOwnership.Instance?.RegisterOwnershipByName(playerId, dupeName);
                }
                
                OniMultiplayerMod.Log($"[Reconnection] Restored {data.OwnedDupeNames.Count} dupe(s) to player '{playerName}'");
            }

            // Remove from disconnected list
            _disconnectedPlayers.Remove(steamId.m_SteamID);

            // Notify all players
            MultiplayerNotification.ShowSuccess($"Player '{playerName}' reconnected!");
            
            OniMultiplayerMod.Log($"[Reconnection] Player '{playerName}' successfully reconnected with ID {playerId}");
        }

        /// <summary>
        /// Called periodically to clean up expired disconnections.
        /// </summary>
        public void Update()
        {
            if (SteamP2PManager.Instance?.IsHost != true) return;
            if (_disconnectedPlayers.Count == 0) return;

            float currentTime = UnityEngine.Time.time;
            var expiredPlayers = new List<ulong>();

            foreach (var kvp in _disconnectedPlayers)
            {
                float elapsed = currentTime - kvp.Value.DisconnectTime;
                if (elapsed > ReconnectionGracePeriod)
                {
                    expiredPlayers.Add(kvp.Key);
                }
            }

            foreach (var steamIdRaw in expiredPlayers)
            {
                var data = _disconnectedPlayers[steamIdRaw];
                
                // Free their dupe assignments
                if (data.OwnedDupeNames != null)
                {
                    foreach (var dupeName in data.OwnedDupeNames)
                    {
                        DupeOwnership.Instance?.UnregisterOwnershipByName(dupeName);
                    }
                }

                _disconnectedPlayers.Remove(steamIdRaw);
                
                OniMultiplayerMod.Log($"[Reconnection] Grace period expired for '{data.PlayerName}'. Their dupes are now unassigned.");
                MultiplayerNotification.ShowInfo($"Player '{data.PlayerName}' timed out. Their dupes are now available.");
            }
        }

        /// <summary>
        /// Get list of disconnected players waiting to reconnect.
        /// </summary>
        public List<(string Name, float TimeLeft)> GetDisconnectedPlayers()
        {
            var result = new List<(string, float)>();
            float currentTime = UnityEngine.Time.time;

            foreach (var data in _disconnectedPlayers.Values)
            {
                float timeLeft = ReconnectionGracePeriod - (currentTime - data.DisconnectTime);
                if (timeLeft > 0)
                {
                    result.Add((data.PlayerName, timeLeft));
                }
            }

            return result;
        }

        /// <summary>
        /// Clear all reconnection data.
        /// </summary>
        public void Clear()
        {
            _disconnectedPlayers.Clear();
        }
    }
}