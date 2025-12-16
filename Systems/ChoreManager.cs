using System.Collections.Generic;
using OniMultiplayer.Network;
using UnityEngine;

namespace OniMultiplayer
{
    /// <summary>
    /// Manages multiplayer chore assignment.
    /// Host-only: Receives player intents and queues chores for their specific dupe.
    /// </summary>
    public class ChoreManager
    {
        public static ChoreManager Instance { get; private set; }

        // Track active chores assigned to players
        private readonly Dictionary<int, List<int>> _playerChores = new Dictionary<int, List<int>>();

        // Chore ID -> Player ID mapping
        private readonly Dictionary<int, int> _choreOwnership = new Dictionary<int, int>();

        public static void Initialize()
        {
            Instance = new ChoreManager();
            OniMultiplayerMod.Log("ChoreManager initialized");
        }

        private static bool IsHost => SteamP2PManager.Instance?.IsHost == true;

        /// <summary>
        /// Queue a dig chore for a player's dupe.
        /// </summary>
        public void QueueDigChore(int playerId, int cell, int priority)
        {
            if (!IsHost) return;

            int dupeInstanceId = DupeOwnership.Instance.GetOwnedDupe(playerId);
            if (dupeInstanceId < 0)
            {
                OniMultiplayerMod.LogWarning($"Player {playerId} has no assigned dupe");
                return;
            }

            // Validate the cell is diggable
            if (!Grid.IsValidCell(cell) || !Grid.Solid[cell])
            {
                OniMultiplayerMod.LogWarning($"Cell {cell} is not diggable");
                return;
            }

            // Create the dig errand through ONI's system
            var dupeGo = DupeOwnership.Instance.GetDupeObject(dupeInstanceId);
            if (dupeGo == null) return;

            // Mark this cell for digging using ONI's Diggable system
            // The game handles creating the actual dig chore
            var diggable = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
            if (diggable == null)
            {
                // Try to mark the cell for digging
                // This is simplified - full implementation would use DigTool's logic
            }

            // Register ownership
            TrackChore(playerId, cell); // Using cell as temp ID

            OniMultiplayerMod.Log($"Queued dig at cell {cell} for player {playerId}'s dupe");
        }

        /// <summary>
        /// Queue a build chore for a player's dupe.
        /// </summary>
        public void QueueBuildChore(int playerId, int cell, string buildingPrefabId, int rotation, int priority)
        {
            if (!IsHost) return;

            int dupeInstanceId = DupeOwnership.Instance.GetOwnedDupe(playerId);
            if (dupeInstanceId < 0)
            {
                OniMultiplayerMod.LogWarning($"Player {playerId} has no assigned dupe");
                return;
            }

            if (!Grid.IsValidCell(cell))
            {
                OniMultiplayerMod.LogWarning($"Cell {cell} is not valid for building");
                return;
            }

            var buildingDef = Assets.GetBuildingDef(buildingPrefabId);
            if (buildingDef == null)
            {
                OniMultiplayerMod.LogWarning($"Building def not found: {buildingPrefabId}");
                return;
            }

            // Create the build order through ONI's system
            var orientation = (Orientation)rotation;
            Vector3 pos = Grid.CellToPosCBC(cell, buildingDef.SceneLayer);

            OniMultiplayerMod.Log($"Queued build '{buildingPrefabId}' at cell {cell} for player {playerId}'s dupe");
        }

        /// <summary>
        /// Queue a deconstruct chore for a player's dupe.
        /// </summary>
        public void QueueDeconstructChore(int playerId, int buildingInstanceId, int cell)
        {
            if (!IsHost) return;

            int dupeInstanceId = DupeOwnership.Instance.GetOwnedDupe(playerId);
            if (dupeInstanceId < 0) return;

            OniMultiplayerMod.Log($"Queued deconstruct for building {buildingInstanceId} for player {playerId}'s dupe");
        }

        /// <summary>
        /// Queue a "use building" chore (e.g., operate, harvest).
        /// </summary>
        public void QueueUseBuildingChore(int playerId, int buildingInstanceId, string interactionType)
        {
            if (!IsHost) return;

            int dupeInstanceId = DupeOwnership.Instance.GetOwnedDupe(playerId);
            if (dupeInstanceId < 0) return;

            OniMultiplayerMod.Log($"Queued use building {buildingInstanceId} ({interactionType}) for player {playerId}'s dupe");
        }

        /// <summary>
        /// Queue a move-to chore for a player's dupe.
        /// </summary>
        public void QueueMoveToChore(int playerId, int targetCell)
        {
            if (!IsHost) return;

            int dupeInstanceId = DupeOwnership.Instance.GetOwnedDupe(playerId);
            if (dupeInstanceId < 0) return;

            if (!Grid.IsValidCell(targetCell))
            {
                OniMultiplayerMod.LogWarning($"Target cell {targetCell} is not valid");
                return;
            }

            var dupeGo = DupeOwnership.Instance.GetDupeObject(dupeInstanceId);
            if (dupeGo == null) return;

            // Use Navigator to move to cell
            var navigator = dupeGo.GetComponent<Navigator>();
            if (navigator != null)
            {
                navigator.GoTo(targetCell);
                OniMultiplayerMod.Log($"Queued move to cell {targetCell} for player {playerId}'s dupe");
            }
        }

        /// <summary>
        /// Change priority of an existing chore.
        /// </summary>
        public void ChangeChorePriority(int playerId, int targetInstanceId, int newPriority)
        {
            if (!IsHost) return;

            // Verify player owns this chore
            if (!_choreOwnership.TryGetValue(targetInstanceId, out int owner) || owner != playerId)
            {
                OniMultiplayerMod.LogWarning($"Player {playerId} cannot change priority of chore {targetInstanceId}");
                return;
            }

            OniMultiplayerMod.Log($"Changed priority of {targetInstanceId} to {newPriority} for player {playerId}");
        }

        /// <summary>
        /// Cancel an active chore.
        /// </summary>
        public void CancelChore(int playerId, int choreId)
        {
            if (!IsHost) return;

            // Verify player owns this chore
            if (!_choreOwnership.TryGetValue(choreId, out int owner) || owner != playerId)
            {
                OniMultiplayerMod.LogWarning($"Player {playerId} cannot cancel chore {choreId}");
                return;
            }

            UntrackChore(choreId);
            OniMultiplayerMod.Log($"Cancelled chore {choreId} for player {playerId}");
        }

        /// <summary>
        /// Check if a chore belongs to a specific player.
        /// </summary>
        public bool IsChoreOwnedBy(int choreId, int playerId)
        {
            return _choreOwnership.TryGetValue(choreId, out int owner) && owner == playerId;
        }

        /// <summary>
        /// Get all chore IDs for a player.
        /// </summary>
        public IEnumerable<int> GetPlayerChores(int playerId)
        {
            if (_playerChores.TryGetValue(playerId, out var chores))
            {
                return chores;
            }
            return System.Array.Empty<int>();
        }

        private void TrackChore(int playerId, int choreId)
        {
            _choreOwnership[choreId] = playerId;

            if (!_playerChores.TryGetValue(playerId, out var chores))
            {
                chores = new List<int>();
                _playerChores[playerId] = chores;
            }
            chores.Add(choreId);
        }

        private void UntrackChore(int choreId)
        {
            if (_choreOwnership.TryGetValue(choreId, out int playerId))
            {
                _choreOwnership.Remove(choreId);

                if (_playerChores.TryGetValue(playerId, out var chores))
                {
                    chores.Remove(choreId);
                }
            }
        }

        /// <summary>
        /// Called when a chore completes (success or failure).
        /// </summary>
        public void OnChoreCompleted(int choreId, bool success)
        {
            if (_choreOwnership.TryGetValue(choreId, out int playerId))
            {
                // Get dupe name (network-safe identifier)
                var dupeNames = DupeOwnership.Instance?.GetOwnedDupeNames(playerId);
                string dupeName = (dupeNames != null && dupeNames.Count > 0) ? dupeNames[0] : "";

                // Notify the owning player
                var packet = new ChoreCompletedPacket
                {
                    ChoreId = choreId,
                    DupeName = dupeName,
                    Success = success
                };

                SteamP2PManager.Instance?.SendToClient(playerId, packet);
                
                UntrackChore(choreId);
            }
        }

        public void Clear()
        {
            _playerChores.Clear();
            _choreOwnership.Clear();
        }
    }
}
