using HarmonyLib;
using OniMultiplayer.Systems;
using UnityEngine;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// CRITICAL PATCHES: Suppress simulation on client machines.
    /// 
    /// WHY THIS EXISTS:
    /// ================
    /// ONI runs a complex simulation every frame:
    /// - Fluid dynamics
    /// - Temperature simulation  
    /// - Gas simulation
    /// - Creature AI
    /// - Dupe AI and pathfinding
    /// - Building operations
    /// - Chore assignment
    /// 
    /// In single-player, this all runs locally. But in multiplayer:
    /// - If both host and client run simulation → DESYNC (different results)
    /// - If only host runs simulation → clients see host's authoritative state
    /// 
    /// These patches DISABLE simulation on clients.
    /// Clients only receive and display state from the host.
    /// </summary>
    public static class SimulationSuppression
    {
        /// <summary>
        /// Block Sim.SIM_HandleMessage on clients.
        /// This is ONI's main simulation tick.
        /// </summary>
        [HarmonyPatch(typeof(Sim), "SIM_HandleMessage")]
        public static class Sim_HandleMessage_Patch
        {
            public static bool Prefix()
            {
                if (ClientMode.ShouldSuppressSimulation)
                {
                    // Don't run simulation on client
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Block SimAndRenderScheduler updates on clients.
        /// This drives the simulation loop.
        /// </summary>
        [HarmonyPatch(typeof(SimAndRenderScheduler), "RenderEveryTick")]
        public static class SimAndRenderScheduler_Patch
        {
            public static bool Prefix()
            {
                if (ClientMode.ShouldSuppressSimulation)
                {
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Block StateMachine updates on clients.
        /// State machines drive dupe AI, building logic, etc.
        /// </summary>
        [HarmonyPatch(typeof(StateMachineUpdater), "AdvanceOneSimSubTick")]
        public static class StateMachineUpdater_Patch
        {
            public static bool Prefix()
            {
                if (ClientMode.ShouldSuppressSimulation)
                {
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Block Chore assignment on clients.
        /// Chores should only be assigned by host.
        /// </summary>
        [HarmonyPatch(typeof(ChoreConsumer), "FindNextChore")]
        public static class ChoreConsumer_FindNextChore_Patch
        {
            public static bool Prefix(ref bool __result)
            {
                if (ClientMode.ShouldSuppressSimulation)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Block pathfinding on clients.
        /// Dupes receive their positions from host.
        /// </summary>
        [HarmonyPatch(typeof(Navigator), "BeginTransition")]
        public static class Navigator_BeginTransition_Patch
        {
            public static bool Prefix()
            {
                if (ClientMode.ShouldSuppressSimulation)
                {
                    return false;
                }
                return true;
            }
        }

        // NOTE: WorldGen.RenderOffline patch removed - WorldGen class may not be accessible
        // World generation is handled by GameBootstrapManager blocking the entire flow for clients

        /// <summary>
        /// Allow render/visual updates to still work on clients.
        /// We only suppress SIMULATION, not rendering.
        /// </summary>
        [HarmonyPatch(typeof(Game), "LateUpdate")]
        public static class Game_LateUpdate_Patch
        {
            public static bool Prefix()
            {
                // Always allow LateUpdate - this handles rendering
                return true;
            }
        }
    }

    /// <summary>
    /// Patches to make sure clients can still receive and display state.
    /// Even though simulation is suppressed, we need visuals to update.
    /// </summary>
    public static class ClientRenderPatches
    {
        /// <summary>
        /// Allow KBatchedAnimController updates on clients.
        /// This displays dupe animations.
        /// </summary>
        [HarmonyPatch(typeof(KBatchedAnimController), "UpdateAnim")]
        public static class KBatchedAnimController_UpdateAnim_Patch
        {
            public static bool Prefix()
            {
                // Always allow animation updates
                return true;
            }
        }

        /// <summary>
        /// Allow position updates from external sources (network).
        /// </summary>
        [HarmonyPatch(typeof(KBatchedAnimController), "SetPositionDirty")]
        public static class KBatchedAnimController_SetPositionDirty_Patch
        {
            public static bool Prefix()
            {
                // Always allow position updates
                return true;
            }
        }
    }
}