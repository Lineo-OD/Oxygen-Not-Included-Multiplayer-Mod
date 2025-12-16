using HarmonyLib;
using OniMultiplayer.Tools;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Patches for UI initialization.
    /// Note: Main menu button is added by MainMenuPatches.cs
    /// </summary>
    public static class UIPatches
    {
        /// <summary>
        /// Initialize debug tools when main menu loads.
        /// </summary>
        [HarmonyPatch(typeof(MainMenu), "OnSpawn")]
        public static class MainMenu_DebugInit_Patch
        {
            public static void Postfix()
            {
                // Initialize F10 method inspector for debugging
                MethodInspector.Initialize();
            }
        }
    }
}
