using LiteNetLib.Utils;

namespace OniMultiplayer
{
    /// <summary>
    /// Sent to client when they first connect.
    /// </summary>
    public class WelcomePacket : INetSerializable
    {
        public int AssignedPlayerId;
        public string HostPlayerName;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(AssignedPlayerId);
            writer.Put(HostPlayerName);
        }

        public void Deserialize(NetDataReader reader)
        {
            AssignedPlayerId = reader.GetInt();
            HostPlayerName = reader.GetString();
        }
    }

    /// <summary>
    /// Notifies clients that a dupe has been assigned to a player.
    /// Uses dupe NAME for network-safe identification (consistent across machines).
    /// </summary>
    public class DupeAssignmentPacket : INetSerializable
    {
        public int PlayerId;
        public string DupeName;  // Network-safe: dupe name is consistent across machines

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(PlayerId);
            writer.Put(DupeName ?? "");
        }

        public void Deserialize(NetDataReader reader)
        {
            PlayerId = reader.GetInt();
            DupeName = reader.GetString();
        }
    }

    /// <summary>
    /// Single dupe state update.
    /// Uses dupe NAME for network-safe identification.
    /// Includes position, animation state, vitals, and timestamp for lag compensation.
    /// </summary>
    public class DupeStatePacket : INetSerializable
    {
        public string DupeName;  // Network-safe: dupe name is consistent across machines
        public float Timestamp;  // Server time for lag compensation
        public float PosX;
        public float PosY;
        public bool FacingRight;
        public string AnimName;  // Direct animation name for reliable sync
        public KAnim.PlayMode AnimMode; // Loop, Once, Paused
        public int CurrentChoreId;

        // Vitals
        public float Stress;
        public float Breath;
        public float Calories;
        public float Health;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(DupeName ?? "");
            writer.Put(Timestamp);
            writer.Put(PosX);
            writer.Put(PosY);
            writer.Put(FacingRight);
            writer.Put(AnimName ?? "idle_loop");
            writer.Put((byte)AnimMode);
            writer.Put(CurrentChoreId);
            writer.Put(Stress);
            writer.Put(Breath);
            writer.Put(Calories);
            writer.Put(Health);
        }

        public void Deserialize(NetDataReader reader)
        {
            DupeName = reader.GetString();
            Timestamp = reader.GetFloat();
            PosX = reader.GetFloat();
            PosY = reader.GetFloat();
            FacingRight = reader.GetBool();
            AnimName = reader.GetString();
            AnimMode = (KAnim.PlayMode)reader.GetByte();
            CurrentChoreId = reader.GetInt();
            Stress = reader.GetFloat();
            Breath = reader.GetFloat();
            Calories = reader.GetFloat();
            Health = reader.GetFloat();
        }
    }

    /// <summary>
    /// Batch update for all dupes - more efficient than individual packets.
    /// </summary>
    public class DupeBatchStatePacket : INetSerializable
    {
        public DupeStatePacket[] Dupes;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Dupes?.Length ?? 0);
            if (Dupes != null)
            {
                foreach (var dupe in Dupes)
                {
                    dupe.Serialize(writer);
                }
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            int count = reader.GetInt();
            Dupes = new DupeStatePacket[count];
            for (int i = 0; i < count; i++)
            {
                Dupes[i] = new DupeStatePacket();
                Dupes[i].Deserialize(reader);
            }
        }
    }

    /// <summary>
    /// World chunk data sent on join.
    /// </summary>
    public class WorldChunkPacket : INetSerializable
    {
        public int ChunkX;
        public int ChunkY;
        public int ChunkWidth;
        public int ChunkHeight;
        public ushort[] Elements;
        public float[] Temperatures;
        public byte[] TileTypes;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(ChunkX);
            writer.Put(ChunkY);
            writer.Put(ChunkWidth);
            writer.Put(ChunkHeight);

            writer.PutArray(Elements);
            writer.PutArray(Temperatures);
            
            // Write byte array manually
            writer.Put(TileTypes.Length);
            foreach (var b in TileTypes)
            {
                writer.Put(b);
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            ChunkX = reader.GetInt();
            ChunkY = reader.GetInt();
            ChunkWidth = reader.GetInt();
            ChunkHeight = reader.GetInt();

            Elements = reader.GetUShortArray();
            Temperatures = reader.GetFloatArray();
            
            // Read byte array manually
            int length = reader.GetInt();
            TileTypes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                TileTypes[i] = reader.GetByte();
            }
        }
    }

    /// <summary>
    /// Single tile update (dig, build, etc).
    /// </summary>
    public class TileUpdatePacket : INetSerializable
    {
        public int Cell;
        public ushort Element;
        public float Temperature;
        public byte TileType;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
            writer.Put(Element);
            writer.Put(Temperature);
            writer.Put(TileType);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
            Element = reader.GetUShort();
            Temperature = reader.GetFloat();
            TileType = reader.GetByte();
        }
    }

    /// <summary>
    /// Building state change.
    /// </summary>
    public class BuildingStatePacket : INetSerializable
    {
        public int BuildingInstanceId;
        public int Cell;
        public string PrefabId;
        public byte State; // 0=created, 1=operational, 2=destroyed
        public int Rotation;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(BuildingInstanceId);
            writer.Put(Cell);
            writer.Put(PrefabId ?? "");
            writer.Put(State);
            writer.Put(Rotation);
        }

        public void Deserialize(NetDataReader reader)
        {
            BuildingInstanceId = reader.GetInt();
            Cell = reader.GetInt();
            PrefabId = reader.GetString();
            State = reader.GetByte();
            Rotation = reader.GetInt();
        }
    }

    /// <summary>
    /// Notifies client that a chore was completed.
    /// </summary>
    public class ChoreCompletedPacket : INetSerializable
    {
        public int ChoreId;
        public string DupeName;  // Network-safe: dupe name is consistent across machines
        public bool Success;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(ChoreId);
            writer.Put(DupeName ?? "");
            writer.Put(Success);
        }

        public void Deserialize(NetDataReader reader)
        {
            ChoreId = reader.GetInt();
            DupeName = reader.GetString();
            Success = reader.GetBool();
        }
    }

    /// <summary>
    /// Sent by host when the game is starting.
    /// Tells clients which save file to load.
    /// </summary>
    public class GameStartPacket : INetSerializable
    {
        public string SaveFileName;     // Name of the save file (just filename, not full path)
        public string WorldName;        // Colony name for display
        public int GameCycle;           // Current game cycle
        public int DupeCount;           // Number of dupes in the game
        public string SaveHash;         // Hash for validation (short 16-char)

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(SaveFileName ?? "");
            writer.Put(WorldName ?? "Colony");
            writer.Put(GameCycle);
            writer.Put(DupeCount);
            writer.Put(SaveHash ?? "");
        }

        public void Deserialize(NetDataReader reader)
        {
            SaveFileName = reader.GetString();
            WorldName = reader.GetString();
            GameCycle = reader.GetInt();
            DupeCount = reader.GetInt();
            SaveHash = reader.GetString();
        }
    }

    /// <summary>
    /// Sent by host after game has loaded and is ready.
    /// Clients should now be in sync with host.
    /// </summary>
    public class GameReadyPacket : INetSerializable
    {
        public float GameTime;          // Current game time for sync
        public bool IsPaused;           // Initial pause state
        public int Speed;               // Initial game speed

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(GameTime);
            writer.Put(IsPaused);
            writer.Put(Speed);
        }

        public void Deserialize(NetDataReader reader)
        {
            GameTime = reader.GetFloat();
            IsPaused = reader.GetBool();
            Speed = reader.GetInt();
        }
    }

    /// <summary>
    /// Sent when a player picks a dupe during character selection.
    /// Broadcast to all players so they see which dupes are taken.
    /// </summary>
    public class DupePickedPacket : INetSerializable
    {
        public int PlayerId;            // Who picked this dupe
        public int ContainerIndex;      // Index of the dupe container
        public string DupeName;         // Name of the dupe for display

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(PlayerId);
            writer.Put(ContainerIndex);
            writer.Put(DupeName ?? "");
        }

        public void Deserialize(NetDataReader reader)
        {
            PlayerId = reader.GetInt();
            ContainerIndex = reader.GetInt();
            DupeName = reader.GetString();
        }
    }

    /// <summary>
    /// Sent when a player unpicks/deselects a dupe during character selection.
    /// </summary>
    public class DupeUnpickedPacket : INetSerializable
    {
        public int PlayerId;            // Who unpicked
        public int ContainerIndex;      // Index of the dupe container

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(PlayerId);
            writer.Put(ContainerIndex);
        }

        public void Deserialize(NetDataReader reader)
        {
            PlayerId = reader.GetInt();
            ContainerIndex = reader.GetInt();
        }
    }

    /// <summary>
    /// Sent by host when all players have selected their dupe and game should proceed.
    /// </summary>
    public class DupeSelectionProceedPacket : INetSerializable
    {
        public void Serialize(NetDataWriter writer)
        {
            // Empty packet - just a signal
        }

        public void Deserialize(NetDataReader reader)
        {
            // Empty packet
        }
    }

    /// <summary>
    /// Sent by client to host when they have finished loading the game.
    /// Host tracks all loaded players and auto-starts when everyone is ready.
    /// </summary>
    public class PlayerLoadedPacket : INetSerializable
    {
        public int PlayerId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(PlayerId);
        }

        public void Deserialize(NetDataReader reader)
        {
            PlayerId = reader.GetInt();
        }
    }

    /// <summary>
    /// Sent by host to all clients when everyone has loaded - game can start.
    /// </summary>
    public class AllPlayersLoadedPacket : INetSerializable
    {
        public int PlayerCount;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(PlayerCount);
        }

        public void Deserialize(NetDataReader reader)
        {
            PlayerCount = reader.GetInt();
        }
    }

    /// <summary>
    /// Sent by host to clients when starting a new game (not loading a save).
    /// Clients should navigate to the new game/world selection screen.
    /// </summary>
    public class NewGameStartPacket : INetSerializable
    {
        public void Serialize(NetDataWriter writer)
        {
            // Empty packet - just a signal
        }

        public void Deserialize(NetDataReader reader)
        {
            // Empty packet
        }
    }

    /// <summary>
    /// Sent by host when all players have selected their dupe and game should actually start.
    /// Different from AllPlayersLoadedPacket - this is after dupe selection is complete.
    /// </summary>
    public class DupeSelectionCompletePacket : INetSerializable
    {
        public void Serialize(NetDataWriter writer)
        {
            // Empty packet - just a signal
        }

        public void Deserialize(NetDataReader reader)
        {
            // Empty packet
        }
    }

    /// <summary>
    /// Bulk assignment of dupes to players. Sent when game starts.
    /// Uses dupe names for network-safe identification.
    /// </summary>
    public class BulkDupeAssignmentPacket : INetSerializable
    {
        public DupeAssignment[] Assignments;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Assignments?.Length ?? 0);
            if (Assignments != null)
            {
                foreach (var assignment in Assignments)
                {
                    writer.Put(assignment.PlayerId);
                    writer.Put(assignment.DupeName ?? "");
                }
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            int count = reader.GetInt();
            Assignments = new DupeAssignment[count];
            for (int i = 0; i < count; i++)
            {
                Assignments[i] = new DupeAssignment
                {
                    PlayerId = reader.GetInt(),
                    DupeName = reader.GetString()
                };
            }
        }
    }

    /// <summary>
    /// Single dupe assignment entry for bulk packet.
    /// Uses dupe name for network-safe identification.
    /// </summary>
    public struct DupeAssignment
    {
        public int PlayerId;
        public string DupeName;  // Network-safe: dupe name is consistent across machines
    }
}