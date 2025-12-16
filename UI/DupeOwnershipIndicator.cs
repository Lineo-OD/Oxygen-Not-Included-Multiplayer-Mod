using UnityEngine;
using System.Collections.Generic;
using OniMultiplayer.Network;

namespace OniMultiplayer.UI
{
    /// <summary>
    /// Visual indicators for dupe ownership in multiplayer.
    /// Shows colored borders/names for dupes based on which player owns them.
    /// </summary>
    public class DupeOwnershipIndicator : MonoBehaviour
    {
        public static DupeOwnershipIndicator Instance { get; private set; }

        // Player colors - distinct and visible
        private static readonly Color[] PlayerColors = new Color[]
        {
            new Color(0.2f, 0.6f, 1.0f, 1f),   // Player 0 (Host) - Blue
            new Color(0.2f, 0.9f, 0.3f, 1f),   // Player 1 - Green
            new Color(1.0f, 0.5f, 0.1f, 1f),   // Player 2 - Orange
            new Color(0.9f, 0.2f, 0.9f, 1f),   // Player 3 - Purple (extra)
        };

        private static readonly Color UnassignedColor = new Color(0.5f, 0.5f, 0.5f, 0.7f); // Gray

        // Cache for dupe indicators
        private readonly Dictionary<string, DupeIndicatorData> _indicators = 
            new Dictionary<string, DupeIndicatorData>();

        // UI settings
        private bool _showIndicators = true;
        private bool _showInWorldLabels = true;
        private GUIStyle _labelStyle;
        private Texture2D _bgTex;

        private class DupeIndicatorData
        {
            public string DupeName;
            public int OwnerPlayerId;
            public GameObject DupeObject;
            public Vector3 LastPosition;
        }

        public static void Initialize()
        {
            if (Instance != null) return;

            var go = new GameObject("DupeOwnershipIndicator");
            Instance = go.AddComponent<DupeOwnershipIndicator>();
            DontDestroyOnLoad(go);

            OniMultiplayerMod.Log("[UI] DupeOwnershipIndicator initialized");
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            CreateStyles();
        }

        private void CreateStyles()
        {
            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
            _bgTex.Apply();

            _labelStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2)
            };
            _labelStyle.normal.background = _bgTex;
            _labelStyle.normal.textColor = Color.white;
        }

        private void Update()
        {
            if (!IsMultiplayer()) return;
            if (Game.Instance == null) return;

            // Update indicator cache
            UpdateIndicatorCache();
        }

        private void UpdateIndicatorCache()
        {
            // Get all dupes using ONI's global Components class
            var dupes = global::Components.LiveMinionIdentities.Items;
            if (dupes == null) return;

            // Update cache
            var currentDupes = new HashSet<string>();

            foreach (var minion in dupes)
            {
                if (minion == null) continue;

                string dupeName = minion.GetProperName();
                if (string.IsNullOrEmpty(dupeName)) continue;

                currentDupes.Add(dupeName);

                int ownerPlayerId = DupeOwnership.Instance?.GetOwnerPlayerByName(dupeName) ?? -1;

                if (!_indicators.TryGetValue(dupeName, out var data))
                {
                    data = new DupeIndicatorData
                    {
                        DupeName = dupeName,
                        DupeObject = minion.gameObject
                    };
                    _indicators[dupeName] = data;
                }

                data.OwnerPlayerId = ownerPlayerId;
                data.DupeObject = minion.gameObject;
                if (data.DupeObject != null)
                {
                    data.LastPosition = data.DupeObject.transform.position;
                }
            }

            // Remove stale entries
            var toRemove = new List<string>();
            foreach (var kvp in _indicators)
            {
                if (!currentDupes.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
            {
                _indicators.Remove(key);
            }
        }

        private void OnGUI()
        {
            if (!_showIndicators) return;
            if (!_showInWorldLabels) return;
            if (!IsMultiplayer()) return;
            if (Game.Instance == null) return;
            if (Camera.main == null) return;

            // Don't show in menus
            if (PauseScreen.Instance != null && PauseScreen.Instance.IsActive()) return;
            
            // Show "Press SPACE" hint during character selection
            if (ImmigrantScreen.instance != null && ImmigrantScreen.instance.gameObject.activeSelf)
            {
                DrawCharacterSelectionHint();
                return;
            }
            
            // Get panel rect if visible (for overlap avoidance)
            Rect panelRect = Rect.zero;
            bool panelVisible = DupeAssignmentPanel.Instance != null && DupeAssignmentPanel.Instance.IsVisible;
            if (panelVisible)
            {
                panelRect = DupeAssignmentPanel.GetPanelRect();
            }

            foreach (var kvp in _indicators)
            {
                var data = kvp.Value;
                if (data.DupeObject == null) continue;

                // Convert world position to screen position
                Vector3 worldPos = data.LastPosition + Vector3.up * 2f; // Above dupe's head
                Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

                // Check if on screen and in front of camera
                if (screenPos.z < 0) continue;
                if (screenPos.x < 0 || screenPos.x > Screen.width) continue;
                if (screenPos.y < 0 || screenPos.y > Screen.height) continue;

                // Flip Y for GUI coordinates
                screenPos.y = Screen.height - screenPos.y;

                // Get player color
                Color playerColor = GetPlayerColor(data.OwnerPlayerId);
                
                // Draw ownership label
                string label = GetOwnerLabel(data.OwnerPlayerId);
                
                _labelStyle.normal.textColor = playerColor;
                
                Vector2 size = _labelStyle.CalcSize(new GUIContent(label));
                Rect rect = new Rect(
                    screenPos.x - size.x / 2,
                    screenPos.y - size.y / 2,
                    size.x,
                    size.y
                );

                // Skip if overlapping with assignment panel
                if (panelVisible && panelRect.Overlaps(rect))
                {
                    continue;
                }

                // Draw colored border
                DrawBorder(rect, playerColor, 2);
                
                // Draw label
                GUI.Label(rect, label, _labelStyle);
            }
        }

        private void DrawCharacterSelectionHint()
        {
            // Draw hint at bottom of screen
            string hint = "▶ Press SPACE to start, then assign dupes to players";
            
            var hintStyle = new GUIStyle(_labelStyle);
            hintStyle.fontSize = 18;
            hintStyle.fontStyle = FontStyle.Bold;
            hintStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f, 1f); // Green
            
            Vector2 size = hintStyle.CalcSize(new GUIContent(hint));
            float x = (Screen.width - size.x) / 2;
            float y = Screen.height - 80;
            
            Rect rect = new Rect(x - 10, y - 5, size.x + 20, size.y + 10);
            
            // Draw background
            GUI.DrawTexture(rect, _bgTex);
            DrawBorder(rect, new Color(0.4f, 0.9f, 0.5f, 1f), 2);
            
            // Draw text
            GUI.Label(new Rect(x, y, size.x, size.y), hint, hintStyle);
        }

        private void DrawBorder(Rect rect, Color color, int thickness)
        {
            Color oldColor = GUI.color;
            GUI.color = color;

            var tex = Texture2D.whiteTexture;
            // Expand rect for border
            rect.x -= thickness;
            rect.y -= thickness;
            rect.width += thickness * 2;
            rect.height += thickness * 2;

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), tex); // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), tex); // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), tex); // Left
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), tex); // Right

            GUI.color = oldColor;
        }

        private string GetOwnerLabel(int playerId)
        {
            if (playerId < 0) return "?";
            
            int localPlayerId = SteamP2PManager.Instance?.LocalPlayerId ?? -1;
            
            if (playerId == localPlayerId)
            {
                return "★ YOU";
            }
            else if (playerId == 0)
            {
                return "P1 (Host)";
            }
            else
            {
                return $"P{playerId + 1}";
            }
        }

        public static Color GetPlayerColor(int playerId)
        {
            if (playerId < 0) return UnassignedColor;
            if (playerId >= PlayerColors.Length) return PlayerColors[playerId % PlayerColors.Length];
            return PlayerColors[playerId];
        }

        public static string GetPlayerColorHex(int playerId)
        {
            Color c = GetPlayerColor(playerId);
            return $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";
        }

        private bool IsMultiplayer()
        {
            // Check if in lobby OR actively playing multiplayer
            return SteamLobbyManager.Instance?.IsInLobby == true ||
                   SteamP2PManager.Instance?.IsConnected == true || 
                   SteamP2PManager.Instance?.IsHost == true;
        }

        /// <summary>
        /// Toggle ownership indicators on/off.
        /// </summary>
        public void ToggleIndicators()
        {
            _showIndicators = !_showIndicators;
            OniMultiplayerMod.Log($"[UI] Dupe ownership indicators: {(_showIndicators ? "ON" : "OFF")}");
            
            MultiplayerNotification.ShowInfo(
                _showIndicators ? "Dupe ownership indicators: ON" : "Dupe ownership indicators: OFF",
                2f
            );
        }

        /// <summary>
        /// Check if indicators are enabled.
        /// </summary>
        public bool AreIndicatorsEnabled => _showIndicators;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}