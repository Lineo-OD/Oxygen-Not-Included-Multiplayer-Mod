using HarmonyLib;
using OniMultiplayer.Network;
using OniMultiplayer.Systems;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Patches to control save/load in multiplayer.
    /// HOST-AUTHORITATIVE ARCHITECTURE:
    /// - Only host can save/load
    /// - Clients receive state from host (they don't load saves locally)
    /// </summary>
    public static class SaveLoadPatches
    {
        private static bool IsMultiplayer => ClientMode.IsMultiplayer;
        private static bool IsHost => ClientMode.IsHost;
        private static bool IsClient => ClientMode.IsClient;

        // Track if client is loading via GameStartPacket (legacy - kept for existing save loading)
        // In true host-authoritative mode, clients wouldn't load saves at all
        public static bool ClientLoadingFromHost { get; set; } = false;

        // Track the current save file path for dupe assignment persistence
        public static string CurrentSavePath { get; set; } = null;

        /// <summary>
        /// Prevent clients from saving.
        /// </summary>
        [HarmonyPatch(typeof(SaveLoader), "Save", typeof(string), typeof(bool), typeof(bool))]
        public static class SaveLoader_Save_Patch
        {
            public static bool Prefix(string filename, bool isAutoSave, bool updateSavePointer)
            {
                if (!IsMultiplayer)
                {
                    return true; // Single player
                }

                if (IsClient)
                {
                    OniMultiplayerMod.LogWarning("[Client] Cannot save - only host can save the game");
                    return false; // Block client saves
                }

                // Host: Allow save
                OniMultiplayerMod.Log($"[Host] Saving game: {filename}");
                return true;
            }
        }

        /// <summary>
        /// Prevent clients from loading saves directly.
        /// In host-authoritative mode, clients receive state from host.
        /// The ClientLoadingFromHost flag is kept for backward compatibility.
        /// </summary>
        [HarmonyPatch(typeof(SaveLoader), "Load", typeof(string))]
        public static class SaveLoader_Load_Patch
        {
            public static bool Prefix(string filename)
            {
                if (!IsMultiplayer)
                {
                    return true; // Single player
                }

                // Allow if client is loading in response to GameStartPacket (legacy flow)
                if (ClientLoadingFromHost)
                {
                    OniMultiplayerMod.Log($"[Client] Loading save from host's request: {filename}");
                    ClientLoadingFromHost = false; // Reset flag
                    return true;
                }

                if (IsClient)
                {
                    OniMultiplayerMod.LogWarning("[Client] Cannot load - only host controls game state");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Disable autosaves on clients - only host should autosave.
        /// </summary>
        [HarmonyPatch(typeof(SaveGame), "get_AutoSaveCycleInterval")]
        public static class SaveGame_AutoSaveCycleInterval_Patch
        {
            public static bool Prefix(ref int __result)
            {
                if (!IsMultiplayer)
                {
                    return true; // Single player - normal autosave
                }

                if (IsClient)
                {
                    // Client: Return -1 to disable autosave
                    __result = -1;
                    return false;
                }

                // Host: Normal autosave behavior
                return true;
            }
        }
        
        /// <summary>
        /// Reset state on cleanup.
        /// </summary>
        public static void Clear()
        {
            ClientLoadingFromHost = false;
            CurrentSavePath = null;
        }
    }
}