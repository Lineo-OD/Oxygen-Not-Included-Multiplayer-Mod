using HarmonyLib;
using OniMultiplayer.Network;
using OniMultiplayer.Systems;
using UnityEngine;
using System.Reflection;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Additional tool patches for common player actions.
    /// All tools inherit from DragTool and override OnDragTool(int cell, int distFromOrigin)
    /// 
    /// HOST-AUTHORITATIVE ARCHITECTURE:
    /// - Clients send tool intents to host
    /// - Host executes the actual game actions
    /// - Results are broadcast back to clients
    /// 
    /// NOTE: Only include patches for tools that have been verified to exist.
    /// </summary>
    public static class ToolPatches
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
        /// Check if we're a client.
        /// </summary>
        private static bool IsClient => ClientMode.IsClient;
        
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
            return 5;
        }

        /// <summary>
        /// Intercept cancel tool to sync cancellations.
        /// Verified: CancelTool exists
        /// </summary>
        [HarmonyPatch(typeof(CancelTool), "OnDragTool")]
        public static class CancelTool_OnDragTool_Patch
        {
            public static bool Prefix(CancelTool __instance, int cell, int distFromOrigin)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                // Client: Send cancel intent
                SendToHost(new RequestCancelAtCellPacket
                {
                    Cell = cell
                });

                OniMultiplayerMod.Log($"[Client] Sent cancel intent for cell {cell}");
                return false;
            }
        }

        /// <summary>
        /// Intercept mop tool (clean up liquids).
        /// Verified: MopTool exists
        /// </summary>
        [HarmonyPatch(typeof(MopTool), "OnDragTool")]
        public static class MopTool_OnDragTool_Patch
        {
            public static bool Prefix(MopTool __instance, int cell, int distFromOrigin)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                SendToHost(new RequestMopPacket
                {
                    Cell = cell,
                    Priority = GetCurrentPriority()
                });

                OniMultiplayerMod.Log($"[Client] Sent mop intent for cell {cell}");
                return false;
            }
        }

        /// <summary>
        /// Intercept clear/sweep tool.
        /// Verified: ClearTool exists
        /// </summary>
        [HarmonyPatch(typeof(ClearTool), "OnDragTool")]
        public static class ClearTool_OnDragTool_Patch
        {
            public static bool Prefix(ClearTool __instance, int cell, int distFromOrigin)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                SendToHost(new RequestSweepPacket
                {
                    Cell = cell,
                    Priority = GetCurrentPriority()
                });

                OniMultiplayerMod.Log($"[Client] Sent sweep intent for cell {cell}");
                return false;
            }
        }

        /// <summary>
        /// Intercept harvest tool.
        /// Verified: HarvestTool exists
        /// </summary>
        [HarmonyPatch(typeof(HarvestTool), "OnDragTool")]
        public static class HarvestTool_OnDragTool_Patch
        {
            public static bool Prefix(HarvestTool __instance, int cell, int distFromOrigin)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                SendToHost(new RequestHarvestPacket
                {
                    Cell = cell,
                    Priority = GetCurrentPriority()
                });

                OniMultiplayerMod.Log($"[Client] Sent harvest intent for cell {cell}");
                return false;
            }
        }

        /// <summary>
        /// Intercept empty pipe tool.
        /// Verified: EmptyPipeTool exists with OnDragTool(Int32 cell, Int32 distFromOrigin)
        /// </summary>
        [HarmonyPatch(typeof(EmptyPipeTool), "OnDragTool")]
        public static class EmptyPipeTool_OnDragTool_Patch
        {
            public static bool Prefix(EmptyPipeTool __instance, int cell, int distFromOrigin)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                SendToHost(new RequestEmptyPipePacket
                {
                    Cell = cell
                });

                OniMultiplayerMod.Log($"[Client] Sent empty pipe intent for cell {cell}");
                return false;
            }
        }

        /// <summary>
        /// Intercept disconnect tool.
        /// Verified: DisconnectTool uses OnDragComplete(Vector3, Vector3) not OnDragTool
        /// </summary>
        [HarmonyPatch(typeof(DisconnectTool), "OnDragComplete")]
        public static class DisconnectTool_OnDragComplete_Patch
        {
            public static bool Prefix(DisconnectTool __instance, Vector3 downPos, Vector3 upPos)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                int startCell = Grid.PosToCell(downPos);
                int endCell = Grid.PosToCell(upPos);

                SendToHost(new RequestDisconnectPacket
                {
                    Cell = startCell
                });

                OniMultiplayerMod.Log($"[Client] Sent disconnect intent from cell {startCell} to {endCell}");
                return false;
            }
        }

        /// <summary>
        /// Intercept capture tool.
        /// Verified: CaptureTool uses OnDragComplete(Vector3, Vector3) not OnDragTool
        /// </summary>
        [HarmonyPatch(typeof(CaptureTool), "OnDragComplete")]
        public static class CaptureTool_OnDragComplete_Patch
        {
            public static bool Prefix(CaptureTool __instance, Vector3 downPos, Vector3 upPos)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                int startCell = Grid.PosToCell(downPos);
                int endCell = Grid.PosToCell(upPos);

                SendToHost(new RequestCapturePacket
                {
                    Cell = startCell
                });

                OniMultiplayerMod.Log($"[Client] Sent capture intent from cell {startCell} to {endCell}");
                return false;
            }
        }

        /// <summary>
        /// Intercept prioritize tool.
        /// Verified: PrioritizeTool uses OnDragTool(Int32 cell, Int32 distFromOrigin)
        /// </summary>
        [HarmonyPatch(typeof(PrioritizeTool), "OnDragTool")]
        public static class PrioritizeTool_OnDragTool_Patch
        {
            public static bool Prefix(PrioritizeTool __instance, int cell, int distFromOrigin)
            {
                if (!IsMultiplayer || IsHost)
                {
                    return true;
                }

                // Get current priority from the tool
                int priority = GetCurrentPriority();

                SendToHost(new RequestPriorityChangePacket
                {
                    TargetCell = cell,
                    TargetInstanceId = -1, // Will be resolved on host
                    NewPriority = priority
                });

                OniMultiplayerMod.Log($"[Client] Sent prioritize intent for cell {cell}, priority {priority}");
                return false;
            }
        }

        // ==============================================================
        // TOOLS NOT ADDED:
        // - AttackTool: Doesn't exist - use FactionAlignment.SetPlayerTargeted()
        // ==============================================================
    }
}