using HarmonyLib;
using OniMultiplayer.Network;
using System.Collections.Generic;
using UnityEngine;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Patches for ONI's character selection screen (ImmigrantScreen).
    /// In multiplayer: Host controls dupe selection. Clients follow along.
    /// After game starts, host assigns dupes to players via DupeAssignmentPanel.
    /// </summary>
    public static class CharacterSelectionPatches
    {
        // Reference to the current screen
        private static CharacterSelectionController _currentController;
        
        // Flag to track if we came from character selection (new game) vs loading a save
        public static bool IsNewGameFromCharacterSelection { get; private set; } = false;
        
        // Flag to track if we're currently IN the character selection screen
        // Game.OnSpawn fires during character selection, before user clicks Embark!
        public static bool IsInCharacterSelection { get; private set; } = false;
        
        // Track if clients are ready (set by proceed signal)
        private static bool _clientProceedAllowed = false;
        
        /// <summary>
        /// Clear the new game flag after game starts (so next load is treated as load game).
        /// </summary>
        public static void ClearNewGameFlag()
        {
            IsNewGameFromCharacterSelection = false;
            IsInCharacterSelection = false;
        }
        
        /// <summary>
        /// Check if we're in multiplayer mode.
        /// </summary>
        private static bool IsMultiplayer()
        {
            return SteamP2PManager.Instance?.IsConnected == true || 
                   SteamP2PManager.Instance?.IsHost == true;
        }
        
        private static bool IsHost()
        {
            return SteamP2PManager.Instance?.IsHost == true;
        }

        /// <summary>
        /// Track controller reference.
        /// </summary>
        [HarmonyPatch(typeof(CharacterSelectionController), "InitializeContainers")]
        public static class CharacterSelectionController_InitializeContainers_Patch
        {
            public static void Postfix(CharacterSelectionController __instance)
            {
                if (!IsMultiplayer()) return;
                
                _currentController = __instance;
                _clientProceedAllowed = false;
                IsInCharacterSelection = true; // We're now in character selection - Game.OnSpawn should NOT show panels yet
                
                if (IsHost())
                {
                    OniMultiplayerMod.Log("[MP] Character selection: Host controls dupe selection (IsInCharacterSelection=true)");
                }
                else
                {
                    OniMultiplayerMod.Log("[MP] Character selection: Waiting for host...");
                }
            }
        }

        /// <summary>
        /// Only host can select dupes. Clients can't interact.
        /// </summary>
        [HarmonyPatch(typeof(CharacterContainer), "SelectDeliverable")]
        public static class CharacterContainer_SelectDeliverable_Patch
        {
            public static bool Prefix(CharacterContainer __instance)
            {
                if (!IsMultiplayer()) return true;
                
                // Only host can select dupes
                if (!IsHost())
                {
                    OniMultiplayerMod.Log("[MP] Only host can select starting dupes");
                    return false; // Block client selection
                }
                
                return true; // Host can select freely
            }
        }

        /// <summary>
        /// Only host can deselect dupes.
        /// </summary>
        [HarmonyPatch(typeof(CharacterContainer), "DeselectDeliverable")]
        public static class CharacterContainer_DeselectDeliverable_Patch
        {
            public static bool Prefix(CharacterContainer __instance)
            {
                if (!IsMultiplayer()) return true;
                
                // Only host can deselect
                if (!IsHost())
                {
                    return false;
                }
                
                return true;
            }
        }

        /// <summary>
        /// Only host can proceed. When host proceeds, signal clients.
        /// </summary>
        [HarmonyPatch(typeof(ImmigrantScreen), "OnProceed")]
        public static class ImmigrantScreen_OnProceed_Patch
        {
            public static bool Prefix()
            {
                if (!IsMultiplayer()) return true;
                
                // Client received proceed signal - allow it
                if (_clientProceedAllowed)
                {
                    IsInCharacterSelection = false;
                    IsNewGameFromCharacterSelection = true;
                    return true;
                }
                
                if (IsHost())
                {
                    // Host proceeds - broadcast to clients
                    IsInCharacterSelection = false; // No longer in character selection
                    IsNewGameFromCharacterSelection = true;
                    
                    var packet = new DupeSelectionProceedPacket();
                    SteamP2PManager.Instance?.BroadcastToClients(packet);
                    
                    OniMultiplayerMod.Log("[MP] Host clicked Embark - signaling clients (IsInCharacterSelection=false)");
                    return true;
                }
                else
                {
                    // Client can't proceed on their own
                    OniMultiplayerMod.Log("[MP] Waiting for host to start the game...");
                    return false;
                }
            }
            
            public static void Postfix()
            {
                if (!IsMultiplayer()) return;
                
                // After Embark is clicked, we need to show the assignment panel
                // But dupes haven't spawned yet! They spawn after the game fully loads.
                // Notify GamePatches to show panel when dupes are ready
                OniMultiplayerMod.Log("[MP] Embark clicked - will show assignment panel after dupes spawn");
                GamePatches.TriggerDupeAssignmentAfterSpawn();
            }
        }
        
        /// <summary>
        /// Called when host signals to proceed.
        /// </summary>
        public static void OnProceedSignal()
        {
            if (_currentController == null) return;
            
            OniMultiplayerMod.Log("[MP] Received proceed signal from host - starting game");
            
            IsNewGameFromCharacterSelection = true;
            _clientProceedAllowed = true;
            
            // Trigger the proceed action
            if (ImmigrantScreen.instance != null)
            {
                var onProceedMethod = AccessTools.Method(typeof(CharacterSelectionController), "OnProceed");
                if (onProceedMethod != null)
                {
                    onProceedMethod.Invoke(_currentController, null);
                }
                else
                {
                    OniMultiplayerMod.LogWarning("[MP] Could not find OnProceed method");
                }
            }
        }
    }
}
