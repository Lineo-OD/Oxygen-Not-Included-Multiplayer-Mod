using HarmonyLib;
using OniMultiplayer.Network;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Patches to control save/load in multiplayer.
    /// Only the host can save/load. Clients follow host's game state.
    /// </summary>
    public static class SaveLoadPatches
    {
        private static bool IsConnected => SteamP2PManager.Instance?.IsConnected == true;
        
        private static bool IsHost => SteamP2PManager.Instance?.IsHost == true;

        // Track if client is loading via GameStartPacket (should be allowed)
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
                if (!IsConnected)
                {
                    return true; // Single player
                }

                if (!IsHost)
                {
                    OniMultiplayerMod.LogWarning("[Client] Cannot save - only host can save the game");
                    return false; // Block client saves
                }

                // Host: Allow save, maybe notify clients
                OniMultiplayerMod.Log($"[Host] Saving game: {filename}");
                return true;
            }
        }

        /// <summary>
        /// Prevent clients from loading saves directly (except when loading from host's GameStartPacket).
        /// </summary>
        [HarmonyPatch(typeof(SaveLoader), "Load", typeof(string))]
        public static class SaveLoader_Load_Patch
        {
            public static bool Prefix(string filename)
            {
                if (!IsConnected)
                {
                    return true; // Single player
                }

                // Allow if client is loading in response to GameStartPacket
                if (ClientLoadingFromHost)
                {
                    OniMultiplayerMod.Log($"[Client] Loading save from host's request: {filename}");
                    ClientLoadingFromHost = false; // Reset flag
                    return true;
                }

                if (!IsHost)
                {
                    OniMultiplayerMod.LogWarning("[Client] Cannot load - only host controls game state");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Disable autosaves on clients - only host should autosave.
        /// Property: SaveGame.AutoSaveCycleInterval (get/set)
        /// </summary>
        [HarmonyPatch(typeof(SaveGame), "get_AutoSaveCycleInterval")]
        public static class SaveGame_AutoSaveCycleInterval_Patch
        {
            public static bool Prefix(ref int __result)
            {
                if (!IsConnected)
                {
                    return true; // Single player - normal autosave
                }

                if (!IsHost)
                {
                    // Client: Return -1 to disable autosave (or very high number)
                    __result = -1;
                    return false;
                }

                // Host: Normal autosave behavior
                return true;
            }
        }
    }
}