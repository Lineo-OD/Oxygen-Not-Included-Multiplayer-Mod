using LiteNetLib.Utils;

namespace OniMultiplayer
{
    /// <summary>
    /// Sent by client when joining with their info.
    /// </summary>
    public class PlayerJoinPacket : INetSerializable
    {
        public string PlayerName;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(PlayerName ?? "Unknown");
        }

        public void Deserialize(NetDataReader reader)
        {
            PlayerName = reader.GetString();
        }
    }

    /// <summary>
    /// Client requests to dig a cell.
    /// </summary>
    public class RequestDigPacket : INetSerializable
    {
        public int Cell;
        public int Priority;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
            writer.Put(Priority);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
            Priority = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to build something.
    /// </summary>
    public class RequestBuildPacket : INetSerializable
    {
        public int Cell;
        public string BuildingPrefabId;
        public int Rotation;
        public int Priority;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
            writer.Put(BuildingPrefabId ?? "");
            writer.Put(Rotation);
            writer.Put(Priority);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
            BuildingPrefabId = reader.GetString();
            Rotation = reader.GetInt();
            Priority = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to deconstruct a building.
    /// </summary>
    public class RequestDeconstructPacket : INetSerializable
    {
        public int BuildingInstanceId;
        public int Cell;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(BuildingInstanceId);
            writer.Put(Cell);
        }

        public void Deserialize(NetDataReader reader)
        {
            BuildingInstanceId = reader.GetInt();
            Cell = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests their dupe to use/interact with a building.
    /// </summary>
    public class RequestUseBuildingPacket : INetSerializable
    {
        public int BuildingInstanceId;
        public string InteractionType; // e.g., "Operate", "Harvest", "Empty"

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(BuildingInstanceId);
            writer.Put(InteractionType ?? "");
        }

        public void Deserialize(NetDataReader reader)
        {
            BuildingInstanceId = reader.GetInt();
            InteractionType = reader.GetString();
        }
    }

    /// <summary>
    /// Client requests their dupe to move to a specific cell.
    /// </summary>
    public class RequestMoveToPacket : INetSerializable
    {
        public int TargetCell;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(TargetCell);
        }

        public void Deserialize(NetDataReader reader)
        {
            TargetCell = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to change priority of an existing chore/errand.
    /// </summary>
    public class RequestPriorityChangePacket : INetSerializable
    {
        public int TargetCell; // Or building instance
        public int TargetInstanceId;
        public int NewPriority;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(TargetCell);
            writer.Put(TargetInstanceId);
            writer.Put(NewPriority);
        }

        public void Deserialize(NetDataReader reader)
        {
            TargetCell = reader.GetInt();
            TargetInstanceId = reader.GetInt();
            NewPriority = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to cancel a chore their dupe is doing.
    /// </summary>
    public class RequestCancelChorePacket : INetSerializable
    {
        public int ChoreId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(ChoreId);
        }

        public void Deserialize(NetDataReader reader)
        {
            ChoreId = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to cancel errands at a cell.
    /// </summary>
    public class RequestCancelAtCellPacket : INetSerializable
    {
        public int Cell;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to mop a cell.
    /// </summary>
    public class RequestMopPacket : INetSerializable
    {
        public int Cell;
        public int Priority;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
            writer.Put(Priority);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
            Priority = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to sweep/clear debris at a cell.
    /// </summary>
    public class RequestSweepPacket : INetSerializable
    {
        public int Cell;
        public int Priority;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
            writer.Put(Priority);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
            Priority = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to harvest at a cell.
    /// </summary>
    public class RequestHarvestPacket : INetSerializable
    {
        public int Cell;
        public int Priority;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
            writer.Put(Priority);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
            Priority = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to attack at a cell.
    /// </summary>
    public class RequestAttackPacket : INetSerializable
    {
        public int Cell;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to capture critter at a cell.
    /// </summary>
    public class RequestCapturePacket : INetSerializable
    {
        public int Cell;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to empty pipe at a cell.
    /// </summary>
    public class RequestEmptyPipePacket : INetSerializable
    {
        public int Cell;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
        }
    }

    /// <summary>
    /// Client requests to disconnect at a cell.
    /// </summary>
    public class RequestDisconnectPacket : INetSerializable
    {
        public int Cell;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Cell);
        }

        public void Deserialize(NetDataReader reader)
        {
            Cell = reader.GetInt();
        }
    }
}