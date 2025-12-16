using HarmonyLib;
using OniMultiplayer.Components;
using OniMultiplayer.Network;
using OniMultiplayer.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Patches for game lifecycle events.
    /// Initialize systems at the right time.
    /// Host controls dupe assignment - players don't self-select.
    /// </summary>
    public static class GamePatches
    {
        // Track which players have loaded the game
        private static HashSet<int> _loadedPlayers = new HashSet<int>();
        
        // Track known dupe NAMES to detect new spawns (using names for network safety)
        private static HashSet<string> _knownDupeNames = new HashSet<string>();
        
        // Flag to suppress NewDupePopup during initial spawn phase
        // True when starting game, false once initial assignment is done
        private static bool _initialSpawnPhase = false;
        
        // Flag to track if we need to show panel once dupes spawn
        private static bool _pendingShowPanel = false;
        private static System.Action _pendingPanelCallback = null;
        
        /// <summary>
        /// Set by DupeAssignmentPanel when host clicks "Done" - ends initial spawn phase.
        /// </summary>
        public static void EndInitialSpawnPhase()
        {
            _initialSpawnPhase = false;
            OniMultiplayerMod.Log("[MP] Initial spawn phase ended - future dupes will trigger popup");
        }
        
        /// <summary>
        /// Called by CharacterSelectionPatches after Embark is clicked.
        /// Starts the coroutine to wait for dupes to spawn and show the assignment panel.
        /// </summary>
        public static void TriggerDupeAssignmentAfterSpawn()
        {
            if (SteamP2PManager.Instance?.IsHost != true) return;
            
            OniMultiplayerMod.Log("[MP] TriggerDupeAssignmentAfterSpawn called");
            
            _pendingShowPanel = true;
            _pendingPanelCallback = () => {
                OniMultiplayerMod.Log("[MP] Host completed initial dupe assignment");
                DupeCameraFollow.Instance?.SnapToMyDupe();
            };
            
            // Start coroutine to wait for dupes to spawn
            if (Game.Instance != null)
            {
                Game.Instance.StartCoroutine(WaitForDupesAndShowPanel());
            }
        }
        
        /// <summary>
        /// Called when a player signals they have loaded.
        /// </summary>
        public static void OnPlayerLoaded(int playerId)
        {
            _loadedPlayers.Add(playerId);
            OniMultiplayerMod.Log($"[MP] Player {playerId} has loaded. ({_loadedPlayers.Count} players loaded)");
            
            // Check if all players are loaded (host only)
            if (SteamP2PManager.Instance?.IsHost == true)
            {
                CheckAllPlayersLoaded();
            }
        }
        
        /// <summary>
        /// Check if all connected players have loaded.
        /// </summary>
        private static void CheckAllPlayersLoaded()
        {
            var allPlayerIds = SteamP2PManager.Instance?.GetAllPlayerIds();
            if (allPlayerIds == null) return;
            
            foreach (int playerId in allPlayerIds)
            {
                if (!_loadedPlayers.Contains(playerId))
                {
                    return; // Not all loaded yet
                }
            }
            
            // All players loaded - host can now assign dupes
            OniMultiplayerMod.Log("[MP] All players have loaded! Host will assign dupes...");
            
            // Broadcast to all clients that everyone has loaded
            var packet = new AllPlayersLoadedPacket { PlayerCount = _loadedPlayers.Count };
            SteamP2PManager.Instance?.BroadcastToClients(packet);
            
            // Show dupe assignment panel to host (already initialized in Game_OnSpawn_Patch)
            DupeAssignmentPanel.Instance?.Show(() => {
                OniMultiplayerMod.Log("[MP] Host completed dupe assignment - game starting!");
                DupeCameraFollow.Instance?.SnapToMyDupe();
            });
        }
        
        /// <summary>
        /// Called when host signals all players are loaded.
        /// Client waits for dupe assignment from host.
        /// </summary>
        public static void OnAllPlayersLoaded()
        {
            OniMultiplayerMod.Log("[MP] All players loaded - waiting for host to assign dupes");
        }
        
        /// <summary>
        /// Send signal that this player has finished loading.
        /// </summary>
        private static void SendPlayerLoadedSignal()
        {
            int localPlayerId = SteamP2PManager.Instance?.LocalPlayerId ?? 0;
            
            OniMultiplayerMod.Log($"[MP] Sending player loaded signal (player {localPlayerId})");
            
            // Track ourselves as loaded
            _loadedPlayers.Add(localPlayerId);
            
            if (SteamP2PManager.Instance?.IsHost == true)
            {
                // Host just checks if everyone is loaded
                CheckAllPlayersLoaded();
            }
            else
            {
                // Client sends to host
                var packet = new PlayerLoadedPacket { PlayerId = localPlayerId };
                SteamP2PManager.Instance?.SendToHost(packet);
            }
        }

        /// <summary>
        /// Initialize multiplayer systems when the game starts.
        /// </summary>
        [HarmonyPatch(typeof(Game), "OnSpawn")]
        public static class Game_OnSpawn_Patch
        {
            public static void Postfix()
            {
                OniMultiplayerMod.Log("Game started - initializing multiplayer systems");

                // Initialize core systems
                DupeOwnership.Initialize();
                DupeSyncManager.Initialize();
                ChoreManager.Initialize();
                WorldSyncManager.Initialize();
                NetworkUpdater.Initialize();
                
                // Reset tracking - we're in initial spawn phase until host clicks Done
                _knownDupeNames.Clear();
                _initialSpawnPhase = true;
                
                // Register all current dupes
                foreach (var minion in global::Components.LiveMinionIdentities.Items)
                {
                    if (minion != null)
                    {
                        string dupeName = minion.GetProperName();
                        if (!string.IsNullOrEmpty(dupeName))
                        {
                            _knownDupeNames.Add(dupeName);
                        }
                        DupeOwnership.Instance?.RegisterDupeObject(minion.gameObject);
                    }
                }
                
                // If we're in a multiplayer session
                bool isMultiplayer = SteamP2PManager.Instance?.IsConnected == true || 
                                     SteamP2PManager.Instance?.IsHost == true;
                
                if (isMultiplayer)
                {
                    // Initialize camera follow and vitals panel
                    DupeCameraFollow.Initialize();
                    MyDupePanel.Initialize();
                    DupeAssignmentPanel.Initialize();
                    NewDupePopup.Initialize();
                    
                    bool isHost = SteamP2PManager.Instance?.IsHost == true;
                    bool isInCharacterSelection = CharacterSelectionPatches.IsInCharacterSelection;
                    bool isNewGame = CharacterSelectionPatches.IsNewGameFromCharacterSelection;
                    
                    // If we're still in character selection, DON'T show any panels yet!
                    // The user hasn't clicked Embark yet. Start a coroutine to wait.
                    if (isInCharacterSelection)
                    {
                        OniMultiplayerMod.Log("[MP] Game.OnSpawn fired but still in character selection - starting wait coroutine...");
                        
                        if (isHost && Game.Instance != null)
                        {
                            // Start coroutine that waits for character selection to end
                            _pendingShowPanel = true;
                            _pendingPanelCallback = () => {
                                OniMultiplayerMod.Log("[MP] Host completed initial dupe assignment");
                                DupeCameraFollow.Instance?.SnapToMyDupe();
                            };
                            Game.Instance.StartCoroutine(WaitForCharacterSelectionEnd());
                        }
                        return;
                    }
                    
                    if (isNewGame)
                    {
                        // New game - host clicked Embark, now dupes will spawn
                        OniMultiplayerMod.Log("[MP] New game - Embark was clicked, waiting for dupes to spawn...");
                        
                        if (isHost)
                        {
                            // Don't show panel immediately - dupes haven't spawned yet!
                            // Set flag and wait for dupes to spawn
                            _pendingShowPanel = true;
                            _pendingPanelCallback = () => {
                                OniMultiplayerMod.Log("[MP] Host completed initial dupe assignment");
                                DupeCameraFollow.Instance?.SnapToMyDupe();
                            };
                            
                            // Start coroutine to wait for dupes to spawn
                            if (Game.Instance != null)
                            {
                                Game.Instance.StartCoroutine(WaitForDupesAndShowPanel());
                            }
                        }
                        
                        CharacterSelectionPatches.ClearNewGameFlag();
                    }
                    else
                    {
                        // Loaded save - wait for all players then host assigns
                        OniMultiplayerMod.Log("[MP] Loaded save - waiting for players");
                        
                        // Signal that we've loaded
                        SendPlayerLoadedSignal();
                    }
                }
            }
        }

        /// <summary>
        /// Coroutine that waits for character selection to end, then waits for dupes.
        /// </summary>
        private static IEnumerator WaitForCharacterSelectionEnd()
        {
            OniMultiplayerMod.Log("[MP] WaitForCharacterSelectionEnd started...");
            
            // Wait until IsInCharacterSelection becomes false, or timeout after 60 seconds
            float timeout = 60f;
            float elapsed = 0f;
            
            while (CharacterSelectionPatches.IsInCharacterSelection && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
                
                // Also check if dupes exist and WattsonMessage has appeared (game fully loaded)
                // This is a fallback in case OnProceed patch doesn't fire
                if (global::Components.LiveMinionIdentities.Items.Count > 0)
                {
                    // Check if the game seems to have started (not in character selection UI anymore)
                    if (ImmigrantScreen.instance == null || !ImmigrantScreen.instance.gameObject.activeSelf)
                    {
                        OniMultiplayerMod.Log("[MP] Character selection screen closed (detected via ImmigrantScreen)");
                        CharacterSelectionPatches.ClearNewGameFlag(); // Force clear the flag
                        break;
                    }
                }
            }
            
            if (elapsed >= timeout)
            {
                OniMultiplayerMod.LogWarning("[MP] Timeout waiting for character selection to end!");
            }
            else
            {
                OniMultiplayerMod.Log("[MP] Character selection ended - now waiting for dupes to spawn...");
            }
            
            // Now wait for dupes to actually spawn (they might get recreated)
            yield return WaitForDupesAndShowPanel();
        }

        /// <summary>
        /// Coroutine that waits for dupes to spawn before showing assignment panel.
        /// </summary>
        private static IEnumerator WaitForDupesAndShowPanel()
        {
            OniMultiplayerMod.Log("[MP] Waiting for dupes to spawn...");
            
            // Wait a few frames for dupes to spawn
            for (int i = 0; i < 10; i++)
            {
                yield return null; // Wait one frame
                
                // Check if dupes have spawned
                int dupeCount = global::Components.LiveMinionIdentities.Items.Count;
                if (dupeCount > 0)
                {
                    OniMultiplayerMod.Log($"[MP] Found {dupeCount} dupes, showing assignment panel");
                    break;
                }
            }
            
            // Additional safety wait
            yield return new WaitForSeconds(0.5f);
            
            // Show the panel
            if (_pendingShowPanel)
            {
                _pendingShowPanel = false;
                DupeAssignmentPanel.Instance?.Show(_pendingPanelCallback);
                _pendingPanelCallback = null;
            }
        }

        /// <summary>
        /// Clean up when game ends.
        /// </summary>
        [HarmonyPatch(typeof(Game), "DestroyInstances")]
        public static class Game_DestroyInstances_Patch
        {
            public static void Prefix()
            {
                OniMultiplayerMod.Log("Game ending - cleaning up multiplayer systems");

                // Clean up Steam P2P
                SteamP2PManager.Instance?.Stop();
                
                // Reset lobby state so we can start a new game
                SteamLobbyManager.Instance?.ResetGameState();
                
                DupeOwnership.Instance?.Clear();
                DupeSyncManager.Instance?.Clear();
                ChoreManager.Instance?.Clear();
                WorldSyncManager.Instance?.Clear();
                ChorePatches.Clear();
                
                _knownDupeNames.Clear();
                _loadedPlayers.Clear();
                _pendingShowPanel = false;
                _pendingPanelCallback = null;
            }
        }

        /// <summary>
        /// Detect when new dupes spawn (from printing pod, etc).
        /// Host gets notification to assign them.
        /// Uses dupe NAMES for network-safe tracking.
        /// </summary>
        [HarmonyPatch(typeof(MinionIdentity), "OnSpawn")]
        public static class MinionIdentity_OnSpawn_Patch
        {
            public static void Postfix(MinionIdentity __instance)
            {
                if (DupeOwnership.Instance == null) return;

                // Get the dupe name (network-safe identifier)
                string dupeName = __instance.GetProperName();
                if (string.IsNullOrEmpty(dupeName)) dupeName = __instance.gameObject.name;
                
                // Register the dupe object (maps name <-> GameObject)
                DupeOwnership.Instance.RegisterDupeObject(__instance.gameObject);

                bool isMultiplayer = SteamP2PManager.Instance?.IsConnected == true || 
                                     SteamP2PManager.Instance?.IsHost == true;
                bool isHost = SteamP2PManager.Instance?.IsHost == true;
                
                // Check if this is a NEW dupe (not from initial load)
                bool isNewDupe = !_knownDupeNames.Contains(dupeName) && Game.Instance != null;
                
                if (isNewDupe)
                {
                    _knownDupeNames.Add(dupeName);
                    
                    OniMultiplayerMod.Log($"[MP] NEW dupe spawned: '{dupeName}'");
                    
                    // Host needs to assign this new dupe
                    if (isMultiplayer && isHost)
                    {
                        if (_initialSpawnPhase)
                        {
                            // During initial spawn phase, DupeAssignmentPanel handles assignment
                            // Just add to unassigned pool - no popup
                            OniMultiplayerMod.Log($"[MP] Initial spawn - '{dupeName}' added to assignment panel");
                        }
                        else
                        {
                            // Mid-game spawn (e.g., printing pod) - show popup for immediate assignment
                            NewDupePopup.Instance?.Show(dupeName);
                        }
                    }
                }
                else
                {
                    _knownDupeNames.Add(dupeName);
                    OniMultiplayerMod.Log($"Registered existing dupe: '{dupeName}'");
                }
            }
        }
        
        /// <summary>
        /// Detect when dupes die/are removed.
        /// Uses dupe NAMES for network-safe tracking.
        /// </summary>
        [HarmonyPatch(typeof(MinionIdentity), "OnCleanUp")]
        public static class MinionIdentity_OnCleanUp_Patch
        {
            public static void Prefix(MinionIdentity __instance)
            {
                if (DupeOwnership.Instance == null) return;
                
                string dupeName = __instance.GetProperName();
                if (string.IsNullOrEmpty(dupeName)) dupeName = __instance.gameObject.name;
                
                // Remove from tracking
                _knownDupeNames.Remove(dupeName);
                DupeOwnership.Instance.UnregisterDupe(dupeName);
                
                OniMultiplayerMod.Log($"[MP] Dupe removed: '{dupeName}'");
            }
        }
    }
}
