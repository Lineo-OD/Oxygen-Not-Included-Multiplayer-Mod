using OniMultiplayer.Network;

namespace OniMultiplayer.Systems
{
    /// <summary>
    /// Manages game bootstrap in multiplayer context.
    /// 
    /// PROBLEM:
    /// ONI's game start is a chain of local operations:
    /// MainMenu → GenerateWorld → LoadGameScene → InitializeSimulation
    /// 
    /// None of this is network-aware. When host clicks "Start":
    /// - Host runs world generation locally
    /// - Clients have no idea what to do
    /// - Even if clients try to follow, they create different worlds
    /// 
    /// SOLUTION:
    /// For HOST: Let ONI bootstrap normally, but track state
    /// For CLIENT: Block normal bootstrap, wait for host state
    /// 
    /// NOTE: Harmony patches are in GamePatches.cs to avoid conflicts.
    /// This class contains the state management logic only.
    /// </summary>
    public static class GameBootstrapManager
    {
        private static bool _waitingForHostState = false;

        /// <summary>
        /// True if client is waiting for host to send game state.
        /// </summary>
        public static bool IsWaitingForHostState => _waitingForHostState;

        /// <summary>
        /// Called when host initiates a new game.
        /// Notifies clients to prepare for state reception.
        /// </summary>
        public static void HostInitiatingNewGame()
        {
            if (!ClientMode.IsHost) return;

            OniMultiplayerMod.Log("[Bootstrap] Host initiating new game - clients will wait for state");
            
            // Tell clients a new game is starting
            var packet = new NewGameStartPacket();
            SteamP2PManager.Instance?.BroadcastToClients(packet);
        }

        /// <summary>
        /// Called when host's game has fully loaded.
        /// Host should now send initial world state to clients.
        /// </summary>
        public static void HostGameReady()
        {
            if (!ClientMode.IsHost) return;

            OniMultiplayerMod.Log("[Bootstrap] Host game ready - preparing to send state to clients");
            
            // Send game ready signal with initial sync data
            var readyPacket = new GameReadyPacket
            {
                GameTime = GameClock.Instance?.GetTime() ?? 0f,
                IsPaused = true, // Start paused so clients can catch up
                Speed = 0
            };
            SteamP2PManager.Instance?.BroadcastToClients(readyPacket);

            // Queue initial world state sync
            WorldSyncManager.Instance?.QueueFullWorldSync();
        }

        /// <summary>
        /// Called when client receives new game notification.
        /// Client should NOT start its own game - wait for host state.
        /// </summary>
        public static void ClientReceivedNewGameSignal()
        {
            if (!ClientMode.IsClient) return;

            OniMultiplayerMod.Log("[Bootstrap] Client received new game signal - waiting for host state");
            _waitingForHostState = true;
            
            // DO NOT call MainMenu.NewGame() here!
            // Client will receive world state from host
        }

        /// <summary>
        /// Called when client receives game ready from host.
        /// Client can now enter "viewing" mode.
        /// </summary>
        public static void ClientReceivedGameReady()
        {
            if (!ClientMode.IsClient) return;

            OniMultiplayerMod.Log("[Bootstrap] Client received game ready - entering viewing mode");
            _waitingForHostState = false;
            ClientMode.ClientEnteredGame();
        }

        /// <summary>
        /// Check if we should block ONI's normal game initialization.
        /// Called by GamePatches.
        /// </summary>
        public static bool ShouldBlockGameInit()
        {
            // Clients should not run normal game init
            // They receive state from host instead
            return ClientMode.IsClient && _waitingForHostState;
        }

        /// <summary>
        /// Reset state - call when leaving multiplayer.
        /// </summary>
        public static void Reset()
        {
            _waitingForHostState = false;
        }
    }
}