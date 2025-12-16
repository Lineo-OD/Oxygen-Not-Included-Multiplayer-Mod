using HarmonyLib;
using OniMultiplayer.Network;
using OniMultiplayer.Systems;
using UnityEngine;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Patches for intercepting player input/UI actions.
    /// 
    /// HOST-AUTHORITATIVE ARCHITECTURE:
    /// - Clients: Convert local actions into network intents sent to host
    /// - Host: Execute actions normally
    /// 
    /// Method signatures verified via MethodInspector (F10) on ONI build 701625.
    /// </summary>
    public static class InputPatches
    {
        /// <summary>
        /// Check if we're in a multiplayer session.
        /// </summary>
        private static bool IsMultiplayer => ClientMode.IsMultiplayer;
        
        /// <summary>
        /// Check if we're the host.
        /// </summary>
        private static bool IsHost => ClientMode.IsHost;
        
        /// <summary>
        /// Send a packet to the host via Steam P2P.
        /// </summary>
        private static void SendToHost(LiteNetLib.Utils.INetSerializable packet)
        {
            SteamP2PManager.Instance?.SendToHost(packet);
        }

        private static int GetCurrentPriority()
        {
            try
            {
                var priorityScreen = ToolMenu.Instance?.PriorityScreen;
                if (priorityScreen != null)
                {
                    var priority = priorityScreen.GetLastSelectedPriority();
                    return priority.priority_value;
                }
            }
            catch { }
            return 5; // Default priority
        }

        /// <summary>
        /// Intercept dig tool dragging.
        /// Signature: protected virtual Void OnDragTool(Int32 cell, Int32 distFromOrigin)
        /// </summary>
        [HarmonyPatch(typeof(DigTool), "OnDragTool")]
        public static class DigTool_OnDragTool_Patch
        {
            public static bool Prefix(DigTool __instance, int cell, int distFromOrigin)
            {
                if (!IsMultiplayer || IsHost)
                {
                    // Host or single-player: execute normally
                    return true;
                }

                // Client: Send dig intent to host instead of executing locally
                SendToHost(new RequestDigPacket
                {
                    Cell = cell,
                    Priority = GetCurrentPriority()
                });

                OniMultiplayerMod.Log($"[Client] Sent dig intent for cell {cell}");
                return false; // Don't execute locally on client
            }
        }

        /// <summary>
        /// Intercept build tool dragging.
        /// Signature: protected virtual Void OnDragTool(Int32 cell, Int32 distFromOrigin)
        /// </summary>
        [HarmonyPatch(typeof(BuildTool), "OnDragTool")]
        public static class BuildTool_OnDragTool_Patch
        {
            public static bool Prefix(BuildTool __instance, int cell, int distFromOrigin)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                // Get the building definition using reflection (private field 'def')
                var defField = typeof(BuildTool).GetField("def", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var def = defField?.GetValue(__instance) as BuildingDef;

                if (def == null)
                {
                    OniMultiplayerMod.LogWarning("[Client] BuildTool has no building def");
                    return false;
                }

                // Get orientation
                var orientationField = typeof(BuildTool).GetField("buildingOrientation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var orientation = orientationField != null 
                    ? (Orientation)orientationField.GetValue(__instance) 
                    : Orientation.Neutral;

                // Client: Send build intent to host
                SendToHost(new RequestBuildPacket
                {
                    Cell = cell,
                    BuildingPrefabId = def.PrefabID,
                    Rotation = (int)orientation,
                    Priority = GetCurrentPriority()
                });

                OniMultiplayerMod.Log($"[Client] Sent build intent for {def.PrefabID} at cell {cell}");
                return false;
            }
        }

        /// <summary>
        /// Intercept deconstruction queueing.
        /// Signature: public Void QueueDeconstruction(Boolean userTriggered)
        /// </summary>
        [HarmonyPatch(typeof(Deconstructable), "QueueDeconstruction", typeof(bool))]
        public static class Deconstructable_QueueDeconstruction_Patch
        {
            public static bool Prefix(Deconstructable __instance, bool userTriggered)
            {
                // Only intercept user-triggered deconstructions
                if (!userTriggered)
                {
                    return true;
                }

                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                // Client: Send deconstruct intent to host
                int cell = Grid.PosToCell(__instance.transform.position);
                int instanceId = __instance.gameObject.GetInstanceID();

                SendToHost(new RequestDeconstructPacket
                {
                    Cell = cell,
                    BuildingInstanceId = instanceId
                });

                OniMultiplayerMod.Log($"[Client] Sent deconstruct intent for cell {cell}");
                return false;
            }
        }

        /// <summary>
        /// Intercept priority changes.
        /// Signature: public Void SetMasterPriority(PrioritySetting priority)
        /// </summary>
        [HarmonyPatch(typeof(Prioritizable), "SetMasterPriority")]
        public static class Prioritizable_SetMasterPriority_Patch
        {
            public static bool Prefix(Prioritizable __instance, PrioritySetting priority)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                // Client: Send priority change intent
                int cell = Grid.PosToCell(__instance.transform.position);
                SendToHost(new RequestPriorityChangePacket
                {
                    TargetCell = cell,
                    TargetInstanceId = __instance.gameObject.GetInstanceID(),
                    NewPriority = priority.priority_value
                });

                return false;
            }
        }
    }
}