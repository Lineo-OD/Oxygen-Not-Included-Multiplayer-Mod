using UnityEngine;

namespace OniMultiplayer.Systems
{
    /// <summary>
    /// Core system that tracks whether this instance is running as a client (viewer) or host (authoritative).
    /// 
    /// ARCHITECTURE NOTES:
    /// ==================
    /// ONI was designed as a single-player game. Its simulation:
    /// - Runs locally on every machine
    /// - Assumes one player
    /// - Has no concept of network authority
    /// 
    /// For multiplayer to work, we need HOST-AUTHORITATIVE design:
    /// - Host: Runs the actual simulation, processes all game logic
    /// - Client: Does NOT run simulation, only displays state from host
    /// 
    /// This class is the central point that all patches check to determine behavior.
    /// </summary>
    public static class ClientMode
    {
        /// <summary>
        /// True if this machine is a client (NOT the host).
        /// Clients don't run simulation - they display host state.
        /// </summary>
        public static bool IsClient { get; private set; } = false;

        /// <summary>
        /// True if this machine is the host (authoritative).
        /// Host runs the actual game simulation.
        /// </summary>
        public static bool IsHost => !IsClient && IsMultiplayer;

        /// <summary>
        /// True if we're in a multiplayer session at all.
        /// </summary>
        public static bool IsMultiplayer { get; private set; } = false;

        /// <summary>
        /// True if the client is currently in "viewing" mode (game loaded, watching host state).
        /// False if client is still in menu/lobby.
        /// </summary>
        public static bool IsClientInGame { get; private set; } = false;

        /// <summary>
        /// True if simulation should be suppressed on this machine.
        /// This is the key flag that patches check.
        /// </summary>
        public static bool ShouldSuppressSimulation => IsClient && IsClientInGame;

        /// <summary>
        /// Called when entering multiplayer as host.
        /// </summary>
        public static void EnterAsHost()
        {
            IsMultiplayer = true;
            IsClient = false;
            IsClientInGame = false;
            OniMultiplayerMod.Log("[ClientMode] Entered as HOST - simulation ENABLED");
        }

        /// <summary>
        /// Called when entering multiplayer as client.
        /// </summary>
        public static void EnterAsClient()
        {
            IsMultiplayer = true;
            IsClient = true;
            IsClientInGame = false;
            OniMultiplayerMod.Log("[ClientMode] Entered as CLIENT - simulation will be SUPPRESSED in-game");
        }

        /// <summary>
        /// Called when client enters the game world (after loading).
        /// At this point, simulation suppression activates.
        /// </summary>
        public static void ClientEnteredGame()
        {
            if (!IsClient) return;
            
            IsClientInGame = true;
            OniMultiplayerMod.Log("[ClientMode] Client entered game - simulation SUPPRESSED");
        }

        /// <summary>
        /// Called when leaving multiplayer (returning to menu).
        /// </summary>
        public static void Leave()
        {
            IsMultiplayer = false;
            IsClient = false;
            IsClientInGame = false;
            OniMultiplayerMod.Log("[ClientMode] Left multiplayer session");
        }

        /// <summary>
        /// Reset all state - called on game cleanup.
        /// </summary>
        public static void Reset()
        {
            IsMultiplayer = false;
            IsClient = false;
            IsClientInGame = false;
        }
    }
}