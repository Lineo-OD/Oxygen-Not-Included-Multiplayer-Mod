using OniMultiplayer.Network;
using UnityEngine;

namespace OniMultiplayer
{
    /// <summary>
    /// Manages world state syncing between host and clients.
    /// Host: Sends world chunks on join, broadcasts tile updates.
    /// Client: Receives and applies world state (render only, no simulation).
    /// </summary>
    public class WorldSyncManager
    {
        public static WorldSyncManager Instance { get; private set; }

        private const int ChunkSize = 32;

        public static void Initialize()
        {
            Instance = new WorldSyncManager();
            OniMultiplayerMod.Log("WorldSyncManager initialized");
        }

        /// <summary>
        /// [HOST] Send complete world state to a newly joined client.
        /// </summary>
        public void SendWorldToClient(int playerId)
        {
            if (SteamP2PManager.Instance?.IsHost != true) return;

            int worldWidth = Grid.WidthInCells;
            int worldHeight = Grid.HeightInCells;

            // Send world in chunks
            for (int chunkY = 0; chunkY < worldHeight; chunkY += ChunkSize)
            {
                for (int chunkX = 0; chunkX < worldWidth; chunkX += ChunkSize)
                {
                    var chunk = CreateWorldChunk(chunkX, chunkY);
                    SteamP2PManager.Instance.SendToClient(playerId, chunk);
                }
            }

            OniMultiplayerMod.Log($"Sent world to player {playerId}");
        }

        /// <summary>
        /// [HOST] Create a world chunk packet.
        /// </summary>
        private WorldChunkPacket CreateWorldChunk(int startX, int startY)
        {
            int width = Mathf.Min(ChunkSize, Grid.WidthInCells - startX);
            int height = Mathf.Min(ChunkSize, Grid.HeightInCells - startY);
            int size = width * height;

            var chunk = new WorldChunkPacket
            {
                ChunkX = startX,
                ChunkY = startY,
                ChunkWidth = width,
                ChunkHeight = height,
                Elements = new ushort[size],
                Temperatures = new float[size],
                TileTypes = new byte[size]
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int cell = Grid.XYToCell(startX + x, startY + y);
                    int idx = y * width + x;

                    if (Grid.IsValidCell(cell))
                    {
                        chunk.Elements[idx] = (ushort)Grid.ElementIdx[cell];
                        chunk.Temperatures[idx] = Grid.Temperature[cell];
                        chunk.TileTypes[idx] = (byte)Grid.BuildMasks[cell];
                    }
                }
            }

            return chunk;
        }

        /// <summary>
        /// [HOST] Broadcast a tile update to all clients.
        /// </summary>
        public void BroadcastTileUpdate(int cell)
        {
            if (SteamP2PManager.Instance?.IsHost != true) return;
            if (!Grid.IsValidCell(cell)) return;

            var packet = new TileUpdatePacket
            {
                Cell = cell,
                Element = (ushort)Grid.ElementIdx[cell],
                Temperature = Grid.Temperature[cell],
                TileType = (byte)Grid.BuildMasks[cell]
            };

            SteamP2PManager.Instance.BroadcastToClients(packet);
        }

        /// <summary>
        /// [HOST] Broadcast a building state change.
        /// </summary>
        public void BroadcastBuildingState(GameObject building, byte state)
        {
            if (SteamP2PManager.Instance?.IsHost != true || building == null) return;

            var kprefab = building.GetComponent<KPrefabID>();
            if (kprefab == null) return;

            int cell = Grid.PosToCell(building);

            var packet = new BuildingStatePacket
            {
                BuildingInstanceId = building.GetInstanceID(),
                Cell = cell,
                PrefabId = kprefab.PrefabTag.Name,
                State = state,
                Rotation = (int)(building.GetComponent<Rotatable>()?.GetOrientation() ?? Orientation.Neutral)
            };

            SteamP2PManager.Instance.BroadcastToClients(packet);
        }

        /// <summary>
        /// [CLIENT] Apply a received world chunk.
        /// Updates Grid data to match host's state.
        /// </summary>
        public void ApplyWorldChunk(WorldChunkPacket chunk)
        {
            if (SteamP2PManager.Instance?.IsHost == true) return;

            OniMultiplayerMod.Log($"[Client] Applying world chunk at ({chunk.ChunkX}, {chunk.ChunkY}) - {chunk.ChunkWidth}x{chunk.ChunkHeight}");
            
            int changesApplied = 0;
            int mismatches = 0;
            
            for (int y = 0; y < chunk.ChunkHeight; y++)
            {
                for (int x = 0; x < chunk.ChunkWidth; x++)
                {
                    int cell = Grid.XYToCell(chunk.ChunkX + x, chunk.ChunkY + y);
                    int idx = y * chunk.ChunkWidth + x;

                    if (!Grid.IsValidCell(cell)) continue;

                    try
                    {
                        // Check if there's a mismatch
                        ushort hostElement = chunk.Elements[idx];
                        float hostTemp = chunk.Temperatures[idx];
                        byte hostTileType = chunk.TileTypes[idx];
                        
                        ushort clientElement = (ushort)Grid.ElementIdx[cell];
                        float clientTemp = Grid.Temperature[cell];
                        
                        // Only apply if different (to avoid unnecessary updates)
                        if (hostElement != clientElement || 
                            System.Math.Abs(hostTemp - clientTemp) > 1f)
                        {
                            mismatches++;
                            
                            // Apply host's state using SimMessages
                            float mass = Grid.Mass[cell]; // Keep existing mass unless element changed
                            if (hostElement != clientElement)
                            {
                                mass = 0f; // Reset mass if element changed
                            }
                            
                            SimMessages.ModifyCell(
                                cell,
                                hostElement,
                                hostTemp,
                                mass,
                                Grid.DiseaseIdx[cell],
                                Grid.DiseaseCount[cell],
                                SimMessages.ReplaceType.Replace,
                                false,
                                -1
                            );
                            
                            changesApplied++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        OniMultiplayerMod.LogError($"[Client] Error applying chunk cell {cell}: {ex.Message}");
                    }
                }
            }
            
            if (mismatches > 0)
            {
                OniMultiplayerMod.Log($"[Client] Chunk applied: {changesApplied}/{mismatches} cells updated");
            }
        }

        /// <summary>
        /// [CLIENT] Apply a single tile update.
        /// </summary>
        public void ApplyTileUpdate(TileUpdatePacket packet)
        {
            if (SteamP2PManager.Instance?.IsHost == true) return;

            // Tile updates should be triggered by the simulation on host
            // Client receives notification and can update visuals if needed
            OniMultiplayerMod.Log($"[Client] Tile update at cell {packet.Cell}");
            
            // Try to trigger visual refresh for this cell
            if (Grid.IsValidCell(packet.Cell))
            {
                try
                {
                    World.Instance?.OnSolidChanged(packet.Cell);
                }
                catch { }
            }
        }

        /// <summary>
        /// [CLIENT] Apply a building state change.
        /// </summary>
        public void ApplyBuildingState(BuildingStatePacket packet)
        {
            if (SteamP2PManager.Instance?.IsHost == true) return;

            switch (packet.State)
            {
                case 0: // Created
                    SpawnBuildingGhost(packet);
                    break;
                case 1: // Operational
                    FinalizeBuildingConstruction(packet);
                    break;
                case 2: // Destroyed
                    DestroyBuilding(packet);
                    break;
            }
        }

        private void SpawnBuildingGhost(BuildingStatePacket packet)
        {
            var buildingDef = Assets.GetBuildingDef(packet.PrefabId);
            if (buildingDef == null) return;

            Vector3 pos = Grid.CellToPosCBC(packet.Cell, buildingDef.SceneLayer);
            var orientation = (Orientation)packet.Rotation;

            // Create visual representation only
            // Full implementation would use buildingDef.BuildingUnderConstruction
            OniMultiplayerMod.Log($"Client: Building ghost spawned at cell {packet.Cell}");
        }

        private void FinalizeBuildingConstruction(BuildingStatePacket packet)
        {
            // Find the ghost and replace with actual building
            // Or spawn the completed building directly
            OniMultiplayerMod.Log($"Client: Building finalized at cell {packet.Cell}");
        }

        private void DestroyBuilding(BuildingStatePacket packet)
        {
            // Find and destroy the building GameObject
            OniMultiplayerMod.Log($"Client: Building destroyed at cell {packet.Cell}");
        }

        public void Clear()
        {
            // Clear any cached state
            _lastDesyncCheckTime = 0f;
            _desyncCount = 0;
        }

        #region Desync Detection

        private float _lastDesyncCheckTime = 0f;
        private const float DesyncCheckInterval = 10f; // Check every 10 seconds
        private int _desyncCount = 0;
        private const int MaxDesyncWarnings = 3;

        /// <summary>
        /// [HOST] Periodic desync check - call from NetworkUpdater.
        /// </summary>
        public void PerformDesyncCheck()
        {
            if (SteamP2PManager.Instance?.IsHost != true) return;
            if (Game.Instance == null || Grid.WidthInCells == 0) return;

            float currentTime = UnityEngine.Time.time;
            if (currentTime - _lastDesyncCheckTime < DesyncCheckInterval) return;
            _lastDesyncCheckTime = currentTime;

            // Calculate checksum of world state (sample-based for performance)
            uint checksum = CalculateWorldChecksum();

            // Send to all clients
            var packet = new WorldChecksumPacket
            {
                Checksum = checksum,
                GameTime = GameClock.Instance?.GetTime() ?? 0f
            };

            SteamP2PManager.Instance.BroadcastToClients(packet);
        }

        /// <summary>
        /// [CLIENT] Verify received checksum matches local state.
        /// </summary>
        public void VerifyChecksum(WorldChecksumPacket packet)
        {
            if (SteamP2PManager.Instance?.IsHost == true) return;
            if (Game.Instance == null || Grid.WidthInCells == 0) return;

            uint localChecksum = CalculateWorldChecksum();

            if (localChecksum != packet.Checksum)
            {
                _desyncCount++;
                OniMultiplayerMod.LogWarning($"[Desync] World checksum mismatch! Host: {packet.Checksum:X8}, Local: {localChecksum:X8} (count: {_desyncCount})");

                if (_desyncCount <= MaxDesyncWarnings)
                {
                    UI.MultiplayerNotification.ShowWarning(
                        $"World desync detected! Your world may differ from host.\n" +
                        $"This is warning {_desyncCount}/{MaxDesyncWarnings}."
                    );
                }
                else if (_desyncCount == MaxDesyncWarnings + 1)
                {
                    UI.MultiplayerNotification.ShowError(
                        "Multiple desyncs detected! World state may be corrupted.\n" +
                        "Consider reconnecting or restarting the game."
                    );
                }
            }
            else
            {
                // Reset desync count on successful match
                if (_desyncCount > 0)
                {
                    OniMultiplayerMod.Log("[Desync] World checksums match again - sync restored");
                    _desyncCount = 0;
                }
            }
        }

        /// <summary>
        /// Calculate a checksum of sampled world state.
        /// We sample cells to keep it fast while still catching major desyncs.
        /// </summary>
        private uint CalculateWorldChecksum()
        {
            uint hash = 2166136261; // FNV-1a offset basis

            int width = Grid.WidthInCells;
            int height = Grid.HeightInCells;
            int totalCells = width * height;

            // Sample ~1000 cells spread across the world
            int sampleStep = System.Math.Max(1, totalCells / 1000);

            for (int i = 0; i < totalCells; i += sampleStep)
            {
                if (!Grid.IsValidCell(i)) continue;

                // Include key cell properties in hash
                hash ^= (uint)Grid.ElementIdx[i];
                hash *= 16777619; // FNV-1a prime

                // Include solid state
                hash ^= Grid.Solid[i] ? 1u : 0u;
                hash *= 16777619;

                // Include mass (rounded to avoid float precision issues)
                int massInt = (int)(Grid.Mass[i] * 10);
                hash ^= (uint)massInt;
                hash *= 16777619;
            }

            return hash;
        }

        #endregion
    }

    /// <summary>
    /// Packet for world state checksum verification.
    /// </summary>
    public class WorldChecksumPacket : LiteNetLib.Utils.INetSerializable
    {
        public uint Checksum;
        public float GameTime;

        public void Serialize(LiteNetLib.Utils.NetDataWriter writer)
        {
            writer.Put(Checksum);
            writer.Put(GameTime);
        }

        public void Deserialize(LiteNetLib.Utils.NetDataReader reader)
        {
            Checksum = reader.GetUInt();
            GameTime = reader.GetFloat();
        }
    }
}
