using System;
using System.Collections.Generic;
using LiteNetLib.Utils;
using OniMultiplayer.Patches;

namespace OniMultiplayer
{
    /// <summary>
    /// Registry for all packet types. Maps packet IDs to types for serialization.
    /// </summary>
    public static class PacketRegistry
    {
        private static readonly Dictionary<byte, Type> _idToType = new Dictionary<byte, Type>();
        private static readonly Dictionary<Type, byte> _typeToId = new Dictionary<Type, byte>();
        private static byte _nextId = 0;

        static PacketRegistry()
        {
            // ========================================
            // Host → Client packets (0-99)
            // ========================================
            Register<WelcomePacket>();              // 0
            Register<DupeAssignmentPacket>();       // 1
            Register<DupeStatePacket>();            // 2
            Register<DupeBatchStatePacket>();       // 3
            Register<WorldChunkPacket>();           // 4
            Register<TileUpdatePacket>();           // 5
            Register<BuildingStatePacket>();        // 6
            Register<ChoreCompletedPacket>();       // 7
            
            // Simulation sync packets
            Register<CellDugPacket>();              // 8
            Register<BuildingPlacedPacket>();       // 9
            Register<BuildingDestroyedPacket>();    // 10
            
            // Speed control packets (host → client)
            Register<SpeedChangePacket>();          // 11
            Register<PauseStatePacket>();           // 12
            
            // Dupe list for selection
            Register<DupeListPacket>();             // 13
            
            // Game flow packets
            Register<GameStartPacket>();            // 14
            Register<GameReadyPacket>();            // 15
            
            // Character selection sync packets
            Register<DupePickedPacket>();           // 16
            Register<DupeUnpickedPacket>();         // 17
            Register<DupeSelectionProceedPacket>(); // 18
            
            // Game loading sync packets
            Register<PlayerLoadedPacket>();         // 19
            Register<AllPlayersLoadedPacket>();     // 20
            
            // New game flow packets
            Register<NewGameStartPacket>();         // 21
            Register<DupeSelectionCompletePacket>();// 22
            Register<BulkDupeAssignmentPacket>();   // 23
            Register<WorldChecksumPacket>();        // 24 - Desync detection

            // ========================================
            // Client → Host packets (100+)
            // ========================================
            _nextId = 100;
            Register<PlayerJoinPacket>();           // 100
            Register<RequestDigPacket>();           // 101
            Register<RequestBuildPacket>();         // 102
            Register<RequestDeconstructPacket>();   // 103
            Register<RequestUseBuildingPacket>();   // 104
            Register<RequestMoveToPacket>();        // 105
            Register<RequestPriorityChangePacket>();// 106
            Register<RequestCancelChorePacket>();   // 107
            
            // Additional tool packets
            Register<RequestCancelAtCellPacket>();  // 108
            Register<RequestMopPacket>();           // 109
            Register<RequestSweepPacket>();         // 110
            Register<RequestHarvestPacket>();       // 111
            Register<RequestAttackPacket>();        // 112
            Register<RequestCapturePacket>();       // 113
            Register<RequestEmptyPipePacket>();     // 114
            Register<RequestDisconnectPacket>();    // 115
            
            // Speed control packets (client → host)
            Register<RequestSpeedChangePacket>();   // 116
            Register<RequestPauseTogglePacket>();   // 117
            Register<RequestPausePacket>();         // 118
            
            // Dupe selection
            Register<RequestDupeSelectionPacket>(); // 119
        }

        private static void Register<T>() where T : INetSerializable, new()
        {
            var type = typeof(T);
            _idToType[_nextId] = type;
            _typeToId[type] = _nextId;
            _nextId++;
        }

        public static byte GetPacketId(Type type)
        {
            if (_typeToId.TryGetValue(type, out var id))
            {
                return id;
            }
            OniMultiplayerMod.LogWarning($"Packet type not registered: {type.Name}");
            return 255;
        }

        public static INetSerializable CreatePacket(byte id)
        {
            if (_idToType.TryGetValue(id, out var type))
            {
                return (INetSerializable)Activator.CreateInstance(type);
            }
            return null;
        }
    }

    /// <summary>
    /// Packet containing list of available dupes for selection.
    /// </summary>
    public class DupeListPacket : INetSerializable
    {
        public List<DupeInfo> Dupes = new List<DupeInfo>();

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Dupes.Count);
            foreach (var dupe in Dupes)
            {
                writer.Put(dupe.InstanceId);
                writer.Put(dupe.Name);
                writer.Put(dupe.OwnerPlayerId);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            int count = reader.GetInt();
            Dupes.Clear();
            for (int i = 0; i < count; i++)
            {
                Dupes.Add(new DupeInfo
                {
                    InstanceId = reader.GetInt(),
                    Name = reader.GetString(),
                    OwnerPlayerId = reader.GetInt()
                });
            }
        }
    }

    public class DupeInfo
    {
        public int InstanceId;
        public string Name;
        public int OwnerPlayerId; // -1 if unassigned
    }

    /// <summary>
    /// Client requests to select a specific dupe.
    /// Uses dupe NAME for network-safe identification.
    /// </summary>
    public class RequestDupeSelectionPacket : INetSerializable
    {
        public string DupeName;  // Network-safe: dupe name is consistent across machines

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(DupeName ?? "");
        }

        public void Deserialize(NetDataReader reader)
        {
            DupeName = reader.GetString();
        }
    }
}