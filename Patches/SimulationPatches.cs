using HarmonyLib;
using OniMultiplayer.Network;
using OniMultiplayer.Systems;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Patches to control simulation on clients.
    /// 
    /// HOST-AUTHORITATIVE ARCHITECTURE:
    /// - Clients should NOT run the full simulation - they only render
    /// - Host runs the authoritative simulation and syncs state
    /// - These patches work alongside SimulationSuppression.cs
    /// </summary>
    public static class SimulationPatches
    {
        private static bool IsMultiplayer => ClientMode.IsMultiplayer;
        private static bool IsHost => ClientMode.IsHost;
        private static bool IsClient => ClientMode.IsClient;
        
        private static void BroadcastToClients(LiteNetLib.Utils.INetSerializable packet)
        {
            SteamP2PManager.Instance?.BroadcastToClients(packet);
        }

        /// <summary>
        /// Prevent clients from destroying cells directly.
        /// Only host can modify world.
        /// </summary>
        [HarmonyPatch(typeof(WorldDamage), "DestroyCell")]
        public static class WorldDamage_DestroyCell_Patch
        {
            public static bool Prefix(int cell)
            {
                if (!IsMultiplayer)
                {
                    return true; // Single player
                }

                // Only host can destroy cells
                return IsHost;
            }
        }

        /// <summary>
        /// Prevent clients from applying damage directly.
        /// </summary>
        [HarmonyPatch(typeof(WorldDamage), "ApplyDamage", 
            typeof(int), typeof(float), typeof(int), typeof(string), typeof(string))]
        public static class WorldDamage_ApplyDamage_Patch
        {
            public static bool Prefix(int cell, float amount, int src_cell, string source_name, string pop_text)
            {
                if (!IsMultiplayer)
                {
                    return true;
                }

                return IsHost;
            }
        }

        /// <summary>
        /// Intercept dig completion to sync with clients.
        /// </summary>
        [HarmonyPatch(typeof(WorldDamage), "OnDigComplete")]
        public static class WorldDamage_OnDigComplete_Patch
        {
            public static void Postfix(int cell, float mass, float temperature, ushort element_idx, byte disease_idx, int disease_count)
            {
                if (!IsMultiplayer || !IsHost)
                {
                    return;
                }

                // Host: Notify clients that a cell was dug
                BroadcastToClients(new CellDugPacket
                {
                    Cell = cell,
                    Mass = mass,
                    Temperature = temperature,
                    ElementIdx = element_idx,
                    DiseaseIdx = disease_idx,
                    DiseaseCount = disease_count
                });
            }
        }

        /// <summary>
        /// Notify clients when building is placed (host broadcasts).
        /// </summary>
        [HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
        public static class BuildingComplete_OnSpawn_Patch
        {
            public static void Postfix(BuildingComplete __instance)
            {
                if (!IsMultiplayer || !IsHost)
                {
                    return;
                }

                // Host: Notify clients of new building
                int cell = Grid.PosToCell(__instance.transform.position);
                var rotatable = __instance.GetComponent<Rotatable>();
                BroadcastToClients(new BuildingPlacedPacket
                {
                    Cell = cell,
                    PrefabId = __instance.Def?.PrefabID ?? "",
                    InstanceId = __instance.gameObject.GetInstanceID(),
                    Temperature = __instance.GetComponent<PrimaryElement>()?.Temperature ?? 293f,
                    Rotation = (int)(rotatable?.GetOrientation() ?? Orientation.Neutral)
                });
            }
        }

        /// <summary>
        /// Notify clients when a building is destroyed.
        /// </summary>
        [HarmonyPatch(typeof(BuildingComplete), "OnCleanUp")]
        public static class BuildingComplete_OnCleanUp_Patch
        {
            public static void Prefix(BuildingComplete __instance)
            {
                if (!IsMultiplayer || !IsHost)
                {
                    return;
                }

                int cell = Grid.PosToCell(__instance.transform.position);
                BroadcastToClients(new BuildingDestroyedPacket
                {
                    Cell = cell,
                    InstanceId = __instance.gameObject.GetInstanceID()
                });
            }
        }
    }

    // Packet definitions for simulation sync
    public class CellDugPacket : LiteNetLib.Utils.INetSerializable
    {
        public int Cell;
        public float Mass;
        public float Temperature;
        public ushort ElementIdx;
        public byte DiseaseIdx;
        public int DiseaseCount;

        public void Serialize(LiteNetLib.Utils.NetDataWriter writer)
        {
            writer.Put(Cell);
            writer.Put(Mass);
            writer.Put(Temperature);
            writer.Put(ElementIdx);
            writer.Put(DiseaseIdx);
            writer.Put(DiseaseCount);
        }

        public void Deserialize(LiteNetLib.Utils.NetDataReader reader)
        {
            Cell = reader.GetInt();
            Mass = reader.GetFloat();
            Temperature = reader.GetFloat();
            ElementIdx = reader.GetUShort();
            DiseaseIdx = reader.GetByte();
            DiseaseCount = reader.GetInt();
        }
    }

    public class BuildingPlacedPacket : LiteNetLib.Utils.INetSerializable
    {
        public int Cell;
        public string PrefabId;
        public int InstanceId;
        public float Temperature;
        public int Rotation;

        public void Serialize(LiteNetLib.Utils.NetDataWriter writer)
        {
            writer.Put(Cell);
            writer.Put(PrefabId ?? "");
            writer.Put(InstanceId);
            writer.Put(Temperature);
            writer.Put(Rotation);
        }

        public void Deserialize(LiteNetLib.Utils.NetDataReader reader)
        {
            Cell = reader.GetInt();
            PrefabId = reader.GetString();
            InstanceId = reader.GetInt();
            Temperature = reader.GetFloat();
            Rotation = reader.GetInt();
        }
    }

    public class BuildingDestroyedPacket : LiteNetLib.Utils.INetSerializable
    {
        public int Cell;
        public int InstanceId;

        public void Serialize(LiteNetLib.Utils.NetDataWriter writer)
        {
            writer.Put(Cell);
            writer.Put(InstanceId);
        }

        public void Deserialize(LiteNetLib.Utils.NetDataReader reader)
        {
            Cell = reader.GetInt();
            InstanceId = reader.GetInt();
        }
    }
}