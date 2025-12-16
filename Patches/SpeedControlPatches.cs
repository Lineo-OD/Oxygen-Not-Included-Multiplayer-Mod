using HarmonyLib;
using OniMultiplayer.Network;
using UnityEngine;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Patches to synchronize game speed across all players.
    /// Only the host can control speed; clients receive speed updates.
    /// </summary>
    public static class SpeedControlPatches
    {
        private static bool IsConnected => SteamP2PManager.Instance?.IsConnected == true;
        
        private static bool IsHost => SteamP2PManager.Instance?.IsHost == true;
        
        private static void SendToHost(LiteNetLib.Utils.INetSerializable packet)
        {
            SteamP2PManager.Instance?.SendToHost(packet);
        }
        
        private static void BroadcastToClients(LiteNetLib.Utils.INetSerializable packet)
        {
            SteamP2PManager.Instance?.BroadcastToClients(packet);
        }

        /// <summary>
        /// Intercept speed changes - only host can change speed.
        /// </summary>
        [HarmonyPatch(typeof(SpeedControlScreen), "SetSpeed")]
        public static class SpeedControlScreen_SetSpeed_Patch
        {
            public static bool Prefix(SpeedControlScreen __instance, int Speed)
            {
                if (!IsConnected)
                {
                    return true; // Single player
                }

                if (IsHost)
                {
                    // Host: Allow and broadcast to clients
                    BroadcastToClients(new SpeedChangePacket
                    {
                        Speed = Speed
                    });
                    return true;
                }
                else
                {
                    // Client: Send request to host
                    SendToHost(new RequestSpeedChangePacket
                    {
                        RequestedSpeed = Speed
                    });
                    return false; // Don't change locally
                }
            }
        }

        /// <summary>
        /// Intercept pause toggle.
        /// Signature: TogglePause(Boolean playsound)
        /// </summary>
        [HarmonyPatch(typeof(SpeedControlScreen), "TogglePause")]
        public static class SpeedControlScreen_TogglePause_Patch
        {
            public static bool Prefix(SpeedControlScreen __instance, bool playsound)
            {
                if (!IsConnected)
                {
                    return true;
                }

                if (IsHost)
                {
                    // Host: Allow and broadcast
                    bool isPaused = SpeedControlScreen.Instance.IsPaused;
                    BroadcastToClients(new PauseStatePacket
                    {
                        IsPaused = !isPaused
                    });
                    return true;
                }
                else
                {
                    // Client: Request pause toggle
                    SendToHost(new RequestPauseTogglePacket());
                    return false;
                }
            }
        }

        /// <summary>
        /// Intercept pause.
        /// Signature: Pause(Boolean playSound, Boolean isCrashed)
        /// </summary>
        [HarmonyPatch(typeof(SpeedControlScreen), "Pause")]
        public static class SpeedControlScreen_Pause_Patch
        {
            public static bool Prefix(SpeedControlScreen __instance, bool playSound, bool isCrashed)
            {
                if (!IsConnected)
                {
                    return true;
                }

                if (IsHost)
                {
                    BroadcastToClients(new PauseStatePacket
                    {
                        IsPaused = true
                    });
                    return true;
                }
                else
                {
                    SendToHost(new RequestPausePacket { Pause = true });
                    return false;
                }
            }
        }

        /// <summary>
        /// Intercept unpause.
        /// </summary>
        [HarmonyPatch(typeof(SpeedControlScreen), "Unpause")]
        public static class SpeedControlScreen_Unpause_Patch
        {
            public static bool Prefix(SpeedControlScreen __instance, bool playSound)
            {
                if (!IsConnected)
                {
                    return true;
                }

                if (IsHost)
                {
                    BroadcastToClients(new PauseStatePacket
                    {
                        IsPaused = false
                    });
                    return true;
                }
                else
                {
                    SendToHost(new RequestPausePacket { Pause = false });
                    return false;
                }
            }
        }
    }

    // Packet definitions for speed control
    public class SpeedChangePacket : LiteNetLib.Utils.INetSerializable
    {
        public int Speed;

        public void Serialize(LiteNetLib.Utils.NetDataWriter writer)
        {
            writer.Put(Speed);
        }

        public void Deserialize(LiteNetLib.Utils.NetDataReader reader)
        {
            Speed = reader.GetInt();
        }
    }

    public class RequestSpeedChangePacket : LiteNetLib.Utils.INetSerializable
    {
        public int RequestedSpeed;

        public void Serialize(LiteNetLib.Utils.NetDataWriter writer)
        {
            writer.Put(RequestedSpeed);
        }

        public void Deserialize(LiteNetLib.Utils.NetDataReader reader)
        {
            RequestedSpeed = reader.GetInt();
        }
    }

    public class PauseStatePacket : LiteNetLib.Utils.INetSerializable
    {
        public bool IsPaused;

        public void Serialize(LiteNetLib.Utils.NetDataWriter writer)
        {
            writer.Put(IsPaused);
        }

        public void Deserialize(LiteNetLib.Utils.NetDataReader reader)
        {
            IsPaused = reader.GetBool();
        }
    }

    public class RequestPauseTogglePacket : LiteNetLib.Utils.INetSerializable
    {
        public void Serialize(LiteNetLib.Utils.NetDataWriter writer) { }
        public void Deserialize(LiteNetLib.Utils.NetDataReader reader) { }
    }

    public class RequestPausePacket : LiteNetLib.Utils.INetSerializable
    {
        public bool Pause;

        public void Serialize(LiteNetLib.Utils.NetDataWriter writer)
        {
            writer.Put(Pause);
        }

        public void Deserialize(LiteNetLib.Utils.NetDataReader reader)
        {
            Pause = reader.GetBool();
        }
    }
}