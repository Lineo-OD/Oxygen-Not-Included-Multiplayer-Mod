using HarmonyLib;
using KMod;
using OniMultiplayer.Network;
using OniMultiplayer.Patches;
using OniMultiplayer.UI;
using Steamworks;

// Steam Lobby UI is the only multiplayer UI

namespace OniMultiplayer
{
    /// <summary>
    /// Main entry point for the ONI Multiplayer mod.
    /// Host-authoritative multiplayer where each player controls one dupe.
    /// Uses Steam P2P networking - no port forwarding required!
    /// </summary>
    public class OniMultiplayerMod : UserMod2
    {
        public static OniMultiplayerMod Instance { get; private set; }
        public static Harmony HarmonyInstance { get; private set; }

        public const string ModVersion = "0.1.0";
        public const string ModName = "ONI Multiplayer";

        public override void OnLoad(Harmony harmony)
        {
            Instance = this;
            HarmonyInstance = harmony;

            // Startup banner
            Debug.Log("╔════════════════════════════════════════════╗");
            Debug.Log($"║  {ModName} v{ModVersion}                    ║");
            Debug.Log("║  Play together with friends!               ║");
            Debug.Log("╚════════════════════════════════════════════╝");

            // Check Steam
            if (!SteamManager.Initialized)
            {
                LogError("Steam not initialized! Multiplayer features disabled.");
                LogError("Make sure you're running through Steam.");
            }
            else
            {
                Log($"Steam user: {SteamFriends.GetPersonaName()}");
            }

            // Initialize Steam networking systems
            SteamLobbyManager.Initialize();   // Steam lobbies
            SteamP2PManager.Initialize();     // Steam P2P

            // Apply all Harmony patches
            harmony.PatchAll(typeof(InputPatches).Assembly);

            Log("Harmony patches applied!");
            Log("Mod loaded successfully!");
        }

        public override void OnAllModsLoaded(Harmony harmony, System.Collections.Generic.IReadOnlyList<Mod> mods)
        {
            base.OnAllModsLoaded(harmony, mods);
            
            // Initialize game systems
            NetworkUpdater.Initialize();
            DupeSyncManager.Initialize();
            DupeOwnership.Initialize();
            ChoreManager.Initialize();
            WorldSyncManager.Initialize();
            Systems.ReconnectionManager.Initialize();
            
            // Initialize UI systems
            MultiplayerNotification.Initialize();
            DupeOwnershipIndicator.Initialize();
            
            Log("All systems ready!");
            Log("───────────────────────────────────────────");
            Log("  Click 'Multiplayer' in main menu to start");
            Log("  Use Steam overlay (Shift+Tab) to invite friends");
            Log("───────────────────────────────────────────");
        }

        public static void Log(string message)
        {
            Debug.Log($"[OniMP] {message}");
        }

        public static void LogError(string message)
        {
            Debug.LogError($"[OniMP] ERROR: {message}");
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning($"[OniMP] WARN: {message}");
        }
    }
}