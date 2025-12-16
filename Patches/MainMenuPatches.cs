using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using OniMultiplayer.UI;

namespace OniMultiplayer.Patches
{
    /// <summary>
    /// Adds a "Multiplayer" button to the main menu.
    /// </summary>
    public static class MainMenuPatches
    {
        private static GameObject _multiplayerButton;

        /// <summary>
        /// Patch MainMenu.OnSpawn to add our button after the menu is created.
        /// </summary>
        [HarmonyPatch(typeof(MainMenu), "OnSpawn")]
        public static class MainMenu_OnSpawn_Patch
        {
            public static void Postfix(MainMenu __instance)
            {
                try
                {
                    AddMultiplayerButton(__instance);
                }
                catch (System.Exception ex)
                {
                    OniMultiplayerMod.LogError($"Failed to add Multiplayer button: {ex.Message}");
                }
            }
        }

        private static void AddMultiplayerButton(MainMenu mainMenu)
        {
            // Find the button container
            var buttonParent = FindButtonParent(mainMenu);
            if (buttonParent == null)
            {
                OniMultiplayerMod.LogWarning("Could not find main menu button container");
                return;
            }

            // Find a SIMPLE button to clone (NOT Resume Game which has extra elements)
            var templateButton = FindSimpleButton(buttonParent);
            if (templateButton == null)
            {
                OniMultiplayerMod.LogWarning("Could not find template button to clone");
                return;
            }

            // Clone the button
            _multiplayerButton = Object.Instantiate(templateButton, buttonParent);
            _multiplayerButton.name = "MultiplayerButton";

            // Remove any extra child elements (like save info on Resume button)
            CleanupButtonChildren(_multiplayerButton);

            // Set the button text
            SetButtonText(_multiplayerButton, "MULTIPLAYER");

            // Set up button click handler
            var button = _multiplayerButton.GetComponent<KButton>();
            if (button != null)
            {
                button.ClearOnClick();
                button.onClick += OnMultiplayerButtonClick;
            }
            else
            {
                var unityButton = _multiplayerButton.GetComponent<Button>();
                if (unityButton != null)
                {
                    unityButton.onClick.RemoveAllListeners();
                    unityButton.onClick.AddListener(OnMultiplayerButtonClick);
                }
            }

            // Position after "Resume Game" (index 1) or at top if no resume
            int insertIndex = FindButtonIndex(buttonParent, "ResumeButton");
            if (insertIndex >= 0)
            {
                _multiplayerButton.transform.SetSiblingIndex(insertIndex + 1);
            }
            else
            {
                // Put after first button
                _multiplayerButton.transform.SetSiblingIndex(1);
            }

            _multiplayerButton.SetActive(true);
            OniMultiplayerMod.Log("Multiplayer button added to main menu!");
        }

        private static Transform FindButtonParent(MainMenu mainMenu)
        {
            // Search for the panel containing menu buttons
            return FindButtonParentRecursive(mainMenu.transform);
        }

        private static Transform FindButtonParentRecursive(Transform root)
        {
            foreach (Transform child in root)
            {
                int buttonCount = 0;
                foreach (Transform grandchild in child)
                {
                    if (grandchild.GetComponent<KButton>() != null)
                        buttonCount++;
                }

                if (buttonCount >= 3)
                    return child;

                var result = FindButtonParentRecursive(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Find a simple button (like New Game, Load Game) - NOT Resume which has extra elements.
        /// </summary>
        private static GameObject FindSimpleButton(Transform buttonParent)
        {
            foreach (Transform child in buttonParent)
            {
                if (!child.gameObject.activeSelf) continue;
                if (child.GetComponent<KButton>() == null) continue;

                // Skip "Resume" button - it has extra save info elements
                string name = child.name.ToLower();
                if (name.Contains("resume")) continue;

                // Look for New Game or Load Game
                if (name.Contains("new") || name.Contains("load") || name.Contains("game"))
                {
                    return child.gameObject;
                }
            }

            // Fallback: return any button that's NOT the first one (Resume)
            int index = 0;
            foreach (Transform child in buttonParent)
            {
                if (child.GetComponent<KButton>() != null && child.gameObject.activeSelf)
                {
                    if (index > 0) // Skip first button (Resume)
                        return child.gameObject;
                    index++;
                }
            }

            return null;
        }

        /// <summary>
        /// Remove extra child elements that might show save info.
        /// </summary>
        private static void CleanupButtonChildren(GameObject button)
        {
            // Find and disable any "SaveInfo" or subtitle elements
            foreach (Transform child in button.transform)
            {
                string name = child.name.ToLower();
                
                // Keep the main text/label, remove save info
                if (name.Contains("save") || name.Contains("info") || name.Contains("subtitle") || name.Contains("cycle"))
                {
                    child.gameObject.SetActive(false);
                }
                
                // Also check for secondary LocText that shows save name
                var locTexts = button.GetComponentsInChildren<LocText>();
                if (locTexts.Length > 1)
                {
                    // Keep only the first (main) text
                    for (int i = 1; i < locTexts.Length; i++)
                    {
                        locTexts[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private static void SetButtonText(GameObject button, string text)
        {
            // Find the main LocText and set it
            var locText = button.GetComponentInChildren<LocText>();
            if (locText != null)
            {
                locText.text = text;
                locText.key = ""; // Clear localization key
            }
        }

        private static int FindButtonIndex(Transform parent, string nameContains)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name.ToLower().Contains(nameContains.ToLower()))
                {
                    return i;
                }
            }
            return -1;
        }

        private static void OnMultiplayerButtonClick()
        {
            OniMultiplayerMod.Log("Multiplayer button clicked!");
            MultiplayerScreen.Show();
        }
    }
}
