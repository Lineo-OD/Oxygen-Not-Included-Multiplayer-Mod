using HarmonyLib;
using OniMultiplayer.Network;
using OniMultiplayer.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Patches to control chore assignment in multiplayer.
    /// 
    /// HOST-AUTHORITATIVE ARCHITECTURE:
    /// - Only HOST runs chore logic - clients don't assign chores
    /// - Player-owned dupes ONLY do tasks their owner requested (or survival tasks)
    /// - Unassigned dupes do whatever they want (AI-controlled)
    /// - Survival tasks (eating, sleeping, breathing, etc.) are always allowed
    /// 
    /// Implementation:
    /// - Track player ownership by CELL (dig/build requests are cell-based)
    /// - When a dupe tries to work, check if the target cell belongs to another player
    /// </summary>
    public static class ChorePatches
    {
        private static bool IsMultiplayer => ClientMode.IsMultiplayer;
        private static bool IsHost => ClientMode.IsHost;

        // Track which cells have player-owned work requests
        // Cell -> PlayerId
        private static readonly Dictionary<int, int> _cellOwnership = new Dictionary<int, int>();

        // Chore types that are "survival" - player dupes can always do these
        private static readonly HashSet<string> _survivalChoreTypes = new HashSet<string>
        {
            // Basic survival
            "Eat", "Drink", "Sleep", "Pee", "Emote",
            "RecoverBreath", "ReturnSuitIdle", "ReturnSuitUrgent",
            "EquipSuit", "UnequipSuit", "DropUnusedInventory",
            
            // Health/stress
            "SeekSafeRefuge", "Flee", "BeIncapacitated",
            "WashHands", "Shower", "TakeMedicine",
            "Narcolepsy", "Vomit", "Cough", "Relax",
            
            // Movement
            "MoveTo", "Idle", "Entombed",
            
            // Social
            "Chat", "Hug", "Mourn",
            
            // Automatic tasks
            "RescueIncapacitated", "DeliverFood",
            
            // Toggle errands (automatic)
            "Toggle", "StressHeal", "StressEmote"
        };

        /// <summary>
        /// Register a cell as owned by a player (when they request dig/build/etc).
        /// </summary>
        public static void RegisterCellOwnership(int cell, int playerId)
        {
            if (!Grid.IsValidCell(cell)) return;
            _cellOwnership[cell] = playerId;
            OniMultiplayerMod.Log($"[ChorePatches] Registered cell {cell} for player {playerId}");
        }

        /// <summary>
        /// Unregister cell ownership (when work completes or is cancelled).
        /// </summary>
        public static void UnregisterCell(int cell)
        {
            _cellOwnership.Remove(cell);
        }

        /// <summary>
        /// Get the player who owns work at a cell (-1 if none).
        /// </summary>
        public static int GetCellOwner(int cell)
        {
            return _cellOwnership.TryGetValue(cell, out int owner) ? owner : -1;
        }

        /// <summary>
        /// Check if a chore type is a survival chore (always allowed).
        /// </summary>
        private static bool IsSurvivalChore(Chore chore)
        {
            if (chore == null || chore.choreType == null) return true;
            
            string choreTypeName = chore.choreType.Id;
            return _survivalChoreTypes.Contains(choreTypeName);
        }

        /// <summary>
        /// Get the target cell for a chore (where the work happens).
        /// </summary>
        private static int GetChoreTargetCell(Chore chore)
        {
            if (chore == null) return -1;

            // Try to get cell from target
            if (chore.target != null)
            {
                return Grid.PosToCell(chore.target.transform.position);
            }

            // Try to get from gameObject
            if (chore.gameObject != null)
            {
                return Grid.PosToCell(chore.gameObject.transform.position);
            }

            return -1;
        }

        /// <summary>
        /// Check if a dupe is allowed to take a specific chore.
        /// </summary>
        private static bool CanDupeTakeChore(int dupeInstanceId, Chore chore)
        {
            if (chore == null) return true;
            
            // Get the dupe's owner
            int dupeOwner = DupeOwnership.Instance?.GetOwnerPlayer(dupeInstanceId) ?? -1;
            
            // If dupe is not player-controlled, allow any chore (AI-controlled)
            if (dupeOwner < 0) return true;
            
            // Survival chores are always allowed
            if (IsSurvivalChore(chore)) return true;
            
            // Get the chore's target cell
            int targetCell = GetChoreTargetCell(chore);
            if (targetCell < 0) return true; // Can't determine, allow
            
            // Get who owns work at this cell
            int cellOwner = GetCellOwner(targetCell);
            
            // If cell is not player-owned, allow (general colony tasks)
            if (cellOwner < 0) return true;
            
            // Only allow if the dupe's owner matches the cell's owner
            return dupeOwner == cellOwner;
        }

        /// <summary>
        /// Main enforcement point: Block dupes from taking chores they shouldn't.
        /// </summary>
        [HarmonyPatch(typeof(ChoreDriver), "SetChore")]
        public static class ChoreDriver_SetChore_Patch
        {
            public static bool Prefix(ChoreDriver __instance, Chore.Precondition.Context context)
            {
                // Only enforce on host in multiplayer
                if (!IsMultiplayer || !IsHost) return true;

                var chore = context.chore;
                if (chore == null) return true;

                int dupeInstanceId = __instance.gameObject.GetInstanceID();
                
                // Check if this dupe can take this chore
                if (!CanDupeTakeChore(dupeInstanceId, chore))
                {
                    int dupeOwner = DupeOwnership.Instance?.GetOwnerPlayer(dupeInstanceId) ?? -1;
                    int targetCell = GetChoreTargetCell(chore);
                    int cellOwner = GetCellOwner(targetCell);
                    
                    // Get dupe name for logging
                    var minionIdentity = __instance.GetComponent<MinionIdentity>();
                    string dupeName = minionIdentity?.GetProperName() ?? "Unknown";
                    string choreType = chore.choreType?.Id ?? "Unknown";
                    
                    OniMultiplayerMod.Log($"[ChorePatches] BLOCKED: '{dupeName}' (P{dupeOwner}) tried {choreType} at cell {targetCell} (owned by P{cellOwner})");
                    
                    return false; // Block the chore assignment
                }

                return true; // Allow the chore
            }

            public static void Postfix(ChoreDriver __instance, Chore.Precondition.Context context)
            {
                if (!IsMultiplayer || !IsHost) return;
                
                var chore = context.chore;
                if (chore == null) return;

                int dupeInstanceId = __instance.gameObject.GetInstanceID();
                int ownerPlayerId = DupeOwnership.Instance?.GetOwnerPlayer(dupeInstanceId) ?? -1;

                if (ownerPlayerId >= 0 && !IsSurvivalChore(chore))
                {
                    var minionIdentity = __instance.GetComponent<MinionIdentity>();
                    string dupeName = minionIdentity?.GetProperName() ?? "Unknown";
                    string choreType = chore.choreType?.Id ?? "Unknown";
                    
                    OniMultiplayerMod.Log($"[ChorePatches] P{ownerPlayerId}'s '{dupeName}' started: {choreType}");
                }
            }
        }

        // Chore filtering is handled by ChoreDriver_SetChore_Patch above.
        // This is the main enforcement point that blocks dupes from taking
        // chores owned by other players.

        /// <summary>
        /// Clean up cell ownership when dig completes.
        /// </summary>
        [HarmonyPatch(typeof(Diggable), "OnSolidChanged")]
        public static class Diggable_OnSolidChanged_Patch
        {
            public static void Postfix(Diggable __instance)
            {
                if (!IsMultiplayer || !IsHost) return;
                
                // If the cell is no longer solid, work is done
                int cell = Grid.PosToCell(__instance.transform.position);
                if (!Grid.Solid[cell])
                {
                    UnregisterCell(cell);
                }
            }
        }

        /// <summary>
        /// Track work completion for logging.
        /// </summary>
        [HarmonyPatch(typeof(Workable), "StartWork")]
        public static class Workable_StartWork_Patch
        {
            public static void Postfix(Workable __instance, object worker_to_start)
            {
                if (!IsMultiplayer || !IsHost) return;
                if (worker_to_start == null) return;

                var workerComponent = worker_to_start as Component;
                if (workerComponent == null) return;

                int dupeInstanceId = workerComponent.gameObject.GetInstanceID();
                int ownerPlayerId = DupeOwnership.Instance?.GetOwnerPlayer(dupeInstanceId) ?? -1;

                if (ownerPlayerId >= 0)
                {
                    var minionIdentity = workerComponent.GetComponent<MinionIdentity>();
                    string dupeName = minionIdentity?.GetProperName() ?? "Unknown";
                    
                    OniMultiplayerMod.Log($"[ChorePatches] P{ownerPlayerId}'s '{dupeName}' working on: {__instance.name}");
                }
            }
        }

        /// <summary>
        /// Clear all tracking data when game ends.
        /// </summary>
        public static void Clear()
        {
            _cellOwnership.Clear();
        }
    }
}
