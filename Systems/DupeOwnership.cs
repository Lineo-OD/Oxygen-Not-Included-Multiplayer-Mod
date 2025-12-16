using System.Collections.Generic;
using System.Linq;
using OniMultiplayer.Network;
using UnityEngine;

namespace OniMultiplayer
{
    /// <summary>
    /// Tracks which player owns which dupes.
    /// Uses dupe NAMES as network identifiers (consistent across machines).
    /// Locally maps names to GameObjects for fast lookup.
    /// </summary>
    public class DupeOwnership
    {
        public static DupeOwnership Instance { get; private set; }

        public const int MaxPlayers = 3;

        // Network-safe identification: DupeName -> PlayerId
        private readonly Dictionary<string, int> _dupeNameToPlayer = new Dictionary<string, int>();
        
        // PlayerId -> List of DupeNames
        private readonly Dictionary<int, List<string>> _playerToDupeNames = new Dictionary<int, List<string>>();
        
        // Local lookup: DupeName -> GameObject (rebuilt on each machine)
        private readonly Dictionary<string, GameObject> _dupeNameToObject = new Dictionary<string, GameObject>();
        
        // Reverse local lookup: InstanceId -> DupeName (for quick local lookups)
        private readonly Dictionary<int, string> _instanceIdToName = new Dictionary<int, string>();

        public static void Initialize()
        {
            Instance = new DupeOwnership();
            OniMultiplayerMod.Log("DupeOwnership initialized (using dupe names for network sync)");
        }

        #region Registration

        /// <summary>
        /// Register a dupe's GameObject. Call this for ALL dupes on game load.
        /// Uses GetProperName() for network-safe identification.
        /// </summary>
        public void RegisterDupeObject(GameObject dupeObject)
        {
            if (dupeObject == null) return;

            var minionIdentity = dupeObject.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return;

            string dupeName = minionIdentity.GetProperName();
            if (string.IsNullOrEmpty(dupeName)) return;

            int instanceId = dupeObject.GetInstanceID();

            _dupeNameToObject[dupeName] = dupeObject;
            _instanceIdToName[instanceId] = dupeName;
        }

        /// <summary>
        /// Legacy method - converts instance ID to name-based registration.
        /// </summary>
        public void RegisterDupeObject(int instanceId, GameObject dupeObject)
        {
            RegisterDupeObject(dupeObject);
        }

        /// <summary>
        /// Unregister a dupe by name.
        /// </summary>
        public void UnregisterDupe(string dupeName)
        {
            if (string.IsNullOrEmpty(dupeName)) return;

            // Find and remove instance ID mapping
            int instanceIdToRemove = -1;
            foreach (var kvp in _instanceIdToName)
            {
                if (kvp.Value == dupeName)
                {
                    instanceIdToRemove = kvp.Key;
                    break;
                }
            }
            if (instanceIdToRemove >= 0)
            {
                _instanceIdToName.Remove(instanceIdToRemove);
            }

            _dupeNameToObject.Remove(dupeName);

            // Also unregister ownership
            UnregisterOwnershipByName(dupeName);
        }

        /// <summary>
        /// Unregister a dupe by instance ID.
        /// </summary>
        public void UnregisterDupe(int instanceId)
        {
            if (_instanceIdToName.TryGetValue(instanceId, out string dupeName))
            {
                UnregisterDupe(dupeName);
            }
        }

        #endregion

        #region Ownership by Name (Network-safe)

        /// <summary>
        /// Register ownership using dupe NAME (network-safe).
        /// This is what should be sent over the network.
        /// </summary>
        public void RegisterOwnershipByName(int playerId, string dupeName)
        {
            if (string.IsNullOrEmpty(dupeName)) return;

            // Remove previous ownership
            if (_dupeNameToPlayer.TryGetValue(dupeName, out int oldPlayerId))
            {
                if (_playerToDupeNames.TryGetValue(oldPlayerId, out var oldList))
                {
                    oldList.Remove(dupeName);
                }
            }

            // Assign to new player
            _dupeNameToPlayer[dupeName] = playerId;

            if (!_playerToDupeNames.TryGetValue(playerId, out var dupeList))
            {
                dupeList = new List<string>();
                _playerToDupeNames[playerId] = dupeList;
            }

            if (!dupeList.Contains(dupeName))
            {
                dupeList.Add(dupeName);
            }

            OniMultiplayerMod.Log($"Registered ownership: Player {playerId} -> '{dupeName}' (owns {dupeList.Count} dupes)");
        }

        /// <summary>
        /// Unregister ownership by name.
        /// </summary>
        public void UnregisterOwnershipByName(string dupeName)
        {
            if (_dupeNameToPlayer.TryGetValue(dupeName, out int playerId))
            {
                _dupeNameToPlayer.Remove(dupeName);

                if (_playerToDupeNames.TryGetValue(playerId, out var dupeList))
                {
                    dupeList.Remove(dupeName);
                }

                OniMultiplayerMod.Log($"Unregistered ownership for dupe '{dupeName}'");
            }
        }

        /// <summary>
        /// Get owner player by dupe name.
        /// </summary>
        public int GetOwnerPlayerByName(string dupeName)
        {
            return _dupeNameToPlayer.TryGetValue(dupeName, out int playerId) ? playerId : -1;
        }

        /// <summary>
        /// Get all dupe names owned by a player.
        /// </summary>
        public List<string> GetOwnedDupeNames(int playerId)
        {
            if (_playerToDupeNames.TryGetValue(playerId, out var dupeList))
            {
                return new List<string>(dupeList);
            }
            return new List<string>();
        }

        #endregion

        #region Ownership by InstanceId (Local convenience)

        /// <summary>
        /// Register ownership using local instance ID (converts to name internally).
        /// </summary>
        public void RegisterOwnership(int playerId, int instanceId)
        {
            string dupeName = GetDupeNameFromInstanceId(instanceId);
            if (!string.IsNullOrEmpty(dupeName))
            {
                RegisterOwnershipByName(playerId, dupeName);
            }
            else
            {
                OniMultiplayerMod.LogWarning($"Cannot register ownership: no dupe name for instance ID {instanceId}");
            }
        }

        /// <summary>
        /// Unregister ownership by instance ID.
        /// </summary>
        public void UnregisterOwnership(int instanceId)
        {
            string dupeName = GetDupeNameFromInstanceId(instanceId);
            if (!string.IsNullOrEmpty(dupeName))
            {
                UnregisterOwnershipByName(dupeName);
            }
        }

        /// <summary>
        /// Get owner player by instance ID.
        /// </summary>
        public int GetOwnerPlayer(int instanceId)
        {
            string dupeName = GetDupeNameFromInstanceId(instanceId);
            return !string.IsNullOrEmpty(dupeName) ? GetOwnerPlayerByName(dupeName) : -1;
        }

        /// <summary>
        /// Get all instance IDs owned by a player (local machine only).
        /// </summary>
        public List<int> GetOwnedDupes(int playerId)
        {
            var result = new List<int>();
            var dupeNames = GetOwnedDupeNames(playerId);
            
            foreach (string name in dupeNames)
            {
                int instanceId = GetInstanceIdFromDupeName(name);
                if (instanceId >= 0)
                {
                    result.Add(instanceId);
                }
            }
            return result;
        }

        /// <summary>
        /// Get the first dupe instance ID owned by a player.
        /// </summary>
        public int GetOwnedDupe(int playerId)
        {
            var dupes = GetOwnedDupes(playerId);
            return dupes.Count > 0 ? dupes[0] : -1;
        }

        /// <summary>
        /// Check if a player owns a specific dupe (by instance ID).
        /// </summary>
        public bool IsOwnedBy(int instanceId, int playerId)
        {
            return GetOwnerPlayer(instanceId) == playerId;
        }

        /// <summary>
        /// Check if a dupe is owned by any player.
        /// </summary>
        public bool IsPlayerControlled(int instanceId)
        {
            return GetOwnerPlayer(instanceId) >= 0;
        }

        /// <summary>
        /// Check if a dupe is unassigned.
        /// </summary>
        public bool IsUnassigned(int instanceId)
        {
            return !IsPlayerControlled(instanceId);
        }

        #endregion

        #region Conversion Utilities

        /// <summary>
        /// Get dupe name from local instance ID.
        /// </summary>
        public string GetDupeNameFromInstanceId(int instanceId)
        {
            return _instanceIdToName.TryGetValue(instanceId, out string name) ? name : null;
        }

        /// <summary>
        /// Get local instance ID from dupe name.
        /// </summary>
        public int GetInstanceIdFromDupeName(string dupeName)
        {
            if (string.IsNullOrEmpty(dupeName)) return -1;

            foreach (var kvp in _instanceIdToName)
            {
                if (kvp.Value == dupeName)
                {
                    return kvp.Key;
                }
            }
            return -1;
        }

        /// <summary>
        /// Get GameObject by dupe name (network-safe lookup).
        /// </summary>
        public GameObject GetDupeObjectByName(string dupeName)
        {
            return _dupeNameToObject.TryGetValue(dupeName, out var obj) ? obj : null;
        }

        /// <summary>
        /// Get GameObject by instance ID (local lookup).
        /// </summary>
        public GameObject GetDupeObject(int instanceId)
        {
            string dupeName = GetDupeNameFromInstanceId(instanceId);
            return !string.IsNullOrEmpty(dupeName) ? GetDupeObjectByName(dupeName) : null;
        }

        #endregion

        #region Queries

        /// <summary>
        /// Get count of dupes owned by a player.
        /// </summary>
        public int GetOwnedDupeCount(int playerId)
        {
            if (_playerToDupeNames.TryGetValue(playerId, out var dupeList))
            {
                return dupeList.Count;
            }
            return 0;
        }

        /// <summary>
        /// Get all registered dupe instance IDs.
        /// </summary>
        public IEnumerable<int> GetAllDupeIds()
        {
            return _instanceIdToName.Keys;
        }

        /// <summary>
        /// Get all registered dupe names.
        /// </summary>
        public IEnumerable<string> GetAllDupeNames()
        {
            return _dupeNameToObject.Keys;
        }

        /// <summary>
        /// Get all unassigned dupe instance IDs.
        /// </summary>
        public List<int> GetUnassignedDupes()
        {
            var unassigned = new List<int>();
            foreach (var kvp in _instanceIdToName)
            {
                if (!_dupeNameToPlayer.ContainsKey(kvp.Value))
                {
                    unassigned.Add(kvp.Key);
                }
            }
            return unassigned;
        }

        /// <summary>
        /// Get local player's first dupe instance ID.
        /// </summary>
        public int GetLocalPlayerDupe()
        {
            int localPlayerId = SteamP2PManager.Instance?.LocalPlayerId ?? -1;
            return GetOwnedDupe(localPlayerId);
        }

        /// <summary>
        /// Get all dupes owned by the local player.
        /// </summary>
        public List<int> GetLocalPlayerDupes()
        {
            int localPlayerId = SteamP2PManager.Instance?.LocalPlayerId ?? -1;
            return GetOwnedDupes(localPlayerId);
        }

        /// <summary>
        /// Check if the local player owns this dupe.
        /// </summary>
        public bool IsLocalPlayerDupe(int instanceId)
        {
            int localPlayerId = SteamP2PManager.Instance?.LocalPlayerId ?? -1;
            return IsOwnedBy(instanceId, localPlayerId);
        }

        /// <summary>
        /// Get all ownership assignments as name-based (for network sync).
        /// Returns dict of playerId -> list of dupeNames.
        /// </summary>
        public Dictionary<int, List<string>> GetAllAssignmentsByName()
        {
            var result = new Dictionary<int, List<string>>();
            foreach (var kvp in _playerToDupeNames)
            {
                result[kvp.Key] = new List<string>(kvp.Value);
            }
            return result;
        }

        /// <summary>
        /// Get all ownership assignments (legacy, uses instance IDs - local only!).
        /// </summary>
        public Dictionary<int, List<int>> GetAllAssignments()
        {
            var result = new Dictionary<int, List<int>>();
            foreach (var kvp in _playerToDupeNames)
            {
                var instanceIds = new List<int>();
                foreach (string name in kvp.Value)
                {
                    int id = GetInstanceIdFromDupeName(name);
                    if (id >= 0) instanceIds.Add(id);
                }
                result[kvp.Key] = instanceIds;
            }
            return result;
        }

        #endregion

        /// <summary>
        /// Clear only ownership assignments (keeps dupe object registrations).
        /// Use this when redistributing dupes.
        /// </summary>
        public void ClearOwnership()
        {
            _dupeNameToPlayer.Clear();
            _playerToDupeNames.Clear();
        }

        /// <summary>
        /// Clear all data.
        /// </summary>
        public void Clear()
        {
            _dupeNameToPlayer.Clear();
            _playerToDupeNames.Clear();
            _dupeNameToObject.Clear();
            _instanceIdToName.Clear();
        }
    }
}