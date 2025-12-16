using UnityEngine;
using OniMultiplayer.Network;
using OniMultiplayer.Systems;

namespace OniMultiplayer
{
    /// <summary>
    /// Unity component that calls network updates every frame.
    /// Handles Steam P2P networking and state synchronization.
    /// 
    /// HOST-AUTHORITATIVE ARCHITECTURE:
    /// - Host: Broadcasts authoritative state at tick rate
    /// - Client: Receives state and updates interpolation (NO simulation)
    /// </summary>
    public class NetworkUpdater : MonoBehaviour
    {
        private static NetworkUpdater _instance;

        // Tick rate for dupe state broadcasts (20 ticks/second)
        private const float DupeSyncTickRate = 0.05f; // 50ms between broadcasts
        private float _timeSinceLastDupeSync = 0f;

        // Tick rate for world state broadcasts (slower, 2 ticks/second)
        private const float WorldSyncTickRate = 0.5f;
        private float _timeSinceLastWorldSync = 0f;

        public static void Initialize()
        {
            if (_instance != null) return;

            var go = new GameObject("OniMultiplayer_NetworkUpdater");
            _instance = go.AddComponent<NetworkUpdater>();
            DontDestroyOnLoad(go);

            OniMultiplayerMod.Log("NetworkUpdater initialized (DupeSync: 20/s, WorldSync: 2/s)");
        }

        private void Update()
        {
            // Poll Steam P2P networking (every frame - receives packets)
            SteamP2PManager.Instance?.Update();

            // Only process game updates if we're in multiplayer
            if (!ClientMode.IsMultiplayer) return;

            if (ClientMode.IsHost)
            {
                // HOST: Broadcast state updates at tick rate
                UpdateHostBroadcasts();
                
                // Update reconnection manager (check for expired disconnections)
                ReconnectionManager.Instance?.Update();
            }
            else if (ClientMode.IsClient && ClientMode.IsClientInGame)
            {
                // CLIENT IN GAME: Update interpolation for smooth dupe movement
                // Client receives state from host and displays it
                DupeSyncManager.Instance?.UpdateInterpolation(Time.deltaTime);
            }
        }

        /// <summary>
        /// Host broadcasts dupe and world state at configured tick rates.
        /// </summary>
        private void UpdateHostBroadcasts()
        {
            // Only broadcast if game is actually running
            if (Game.Instance == null || SpeedControlScreen.Instance == null) return;
            if (SpeedControlScreen.Instance.IsPaused) return;

            // Dupe state sync (20 ticks/second)
            _timeSinceLastDupeSync += Time.deltaTime;
            if (_timeSinceLastDupeSync >= DupeSyncTickRate)
            {
                _timeSinceLastDupeSync = 0f;
                DupeSyncManager.Instance?.BroadcastDupeStates();
            }

            // World state sync - only for delta changes (handled by SimulationPatches)
            // This timer handles periodic desync detection
            _timeSinceLastWorldSync += Time.deltaTime;
            if (_timeSinceLastWorldSync >= WorldSyncTickRate)
            {
                _timeSinceLastWorldSync = 0f;
                
                // Periodic desync check (internal timer handles actual interval)
                WorldSyncManager.Instance?.PerformDesyncCheck();
            }
        }

        private void OnDestroy()
        {
            SteamP2PManager.Instance?.Stop();
            _instance = null;
        }
    }
}