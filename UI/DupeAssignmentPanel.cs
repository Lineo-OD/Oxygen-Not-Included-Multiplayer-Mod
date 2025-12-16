using UnityEngine;
using OniMultiplayer.Network;
using System.Collections.Generic;
using System.Linq;

namespace OniMultiplayer.UI
{
    /// <summary>
    /// Host-only panel for assigning dupes to players.
    /// Shows after loading a save or when new dupes spawn.
    /// The host controls all dupe assignments.
    /// </summary>
    public class DupeAssignmentPanel : MonoBehaviour
    {
        public static DupeAssignmentPanel Instance { get; private set; }

        private bool _isVisible = false;
        public bool IsVisible => _isVisible;
        
        // Panel dimensions for overlap checking
        public static float PanelWidth => 550;
        public static float PanelHeight => 500;
        public static Rect GetPanelRect()
        {
            float x = (Screen.width - PanelWidth) / 2;
            float y = (Screen.height - PanelHeight) / 2;
            return new Rect(x, y, PanelWidth, PanelHeight);
        }
        private Vector2 _scrollPosition;
        
        // Assignment mode
        private int _selectedPlayerId = -1;
        private bool _showUnassignedOnly = false;
        
        // Callback for when assignment is complete
        private System.Action _onComplete;

        // UI colors
        private static readonly Color BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        private static readonly Color PanelColor = new Color(0.14f, 0.14f, 0.18f, 1f);
        private static readonly Color AccentColor = new Color(0.3f, 0.7f, 0.5f, 1f);
        private static readonly Color Player1Color = new Color(0.4f, 0.75f, 0.95f, 1f);
        private static readonly Color Player2Color = new Color(0.95f, 0.65f, 0.4f, 1f);
        private static readonly Color Player3Color = new Color(0.8f, 0.5f, 0.9f, 1f);
        private static readonly Color UnassignedColor = new Color(0.5f, 0.5f, 0.55f, 1f);
        private static readonly Color TextDim = new Color(0.6f, 0.6f, 0.65f, 1f);
        private static readonly Color TextBright = new Color(0.95f, 0.95f, 0.95f, 1f);

        private Texture2D _bgTex;
        private Texture2D _panelTex;
        private Texture2D _accentTex;
        
        // Cached textures for dupe entries (avoid creating every frame)
        private Texture2D _entrySelectedTex;
        private Texture2D _entryUnassignedTex;
        private Texture2D _entryAssignedTex;
        private Texture2D _entryHoverTex;

        public static void Initialize()
        {
            if (Instance != null) return;

            var go = new GameObject("DupeAssignmentPanel");
            Instance = go.AddComponent<DupeAssignmentPanel>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            Instance = this;
            CreateTextures();
        }

        private void CreateTextures()
        {
            _bgTex = MakeTex(BgColor);
            _panelTex = MakeTex(PanelColor);
            _accentTex = MakeTex(AccentColor);
            _entrySelectedTex = MakeTex(new Color(0.25f, 0.4f, 0.3f, 0.9f));
            _entryUnassignedTex = MakeTex(new Color(0.18f, 0.18f, 0.2f, 0.7f));
            _entryAssignedTex = MakeTex(new Color(0.15f, 0.15f, 0.18f, 0.7f));
            _entryHoverTex = MakeTex(new Color(0.3f, 0.5f, 0.4f, 0.3f));
        }

        private Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Show the assignment panel. Only works for host.
        /// </summary>
        public void Show(System.Action onComplete = null)
        {
            if (!IsHost())
            {
                OniMultiplayerMod.Log("[DupeAssignment] Only host can assign dupes");
                return;
            }

            _isVisible = true;
            _onComplete = onComplete;
            _selectedPlayerId = 0; // Default to host

            // Pause the game while assigning
            if (SpeedControlScreen.Instance != null)
            {
                SpeedControlScreen.Instance.Pause(false, false);
            }

            OniMultiplayerMod.Log("[DupeAssignment] Panel shown");
        }

        /// <summary>
        /// Hide the panel and optionally trigger completion callback.
        /// </summary>
        public void Hide(bool triggerComplete = true)
        {
            _isVisible = false;

            // Unpause
            if (SpeedControlScreen.Instance != null)
            {
                SpeedControlScreen.Instance.Unpause(false);
            }

            if (triggerComplete)
            {
                _onComplete?.Invoke();
            }

            OniMultiplayerMod.Log("[DupeAssignment] Panel hidden");
        }

        private bool IsHost()
        {
            return SteamP2PManager.Instance?.IsHost == true;
        }

        private Color GetPlayerColor(int playerId)
        {
            switch (playerId)
            {
                case 0: return Player1Color;
                case 1: return Player2Color;
                case 2: return Player3Color;
                default: return UnassignedColor;
            }
        }

        private string GetPlayerName(int playerId)
        {
            return SteamP2PManager.Instance?.GetPlayerName(playerId) ?? $"Player {playerId}";
        }

        private void OnGUI()
        {
            if (!_isVisible) return;
            if (!IsHost()) return;

            // Full screen dark background
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTex);

            // Center panel
            float panelWidth = 550;
            float panelHeight = 500;
            float x = (Screen.width - panelWidth) / 2;
            float y = (Screen.height - panelHeight) / 2;

            // Panel border
            GUI.DrawTexture(new Rect(x - 2, y - 2, panelWidth + 4, panelHeight + 4), _accentTex);
            GUI.DrawTexture(new Rect(x, y, panelWidth, panelHeight), _panelTex);

            GUILayout.BeginArea(new Rect(x + 20, y + 15, panelWidth - 40, panelHeight - 30));

            DrawHeader();
            GUILayout.Space(12);
            DrawPlayerSelector();
            GUILayout.Space(12);
            DrawDupeList();
            GUILayout.Space(12);
            DrawActions();

            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("ASSIGN DUPES TO PLAYERS", GetTitleStyle());
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Click a dupe to assign it to the selected player", GetDescStyle());
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawPlayerSelector()
        {
            GUILayout.Label("SELECT PLAYER:", GetSectionStyle());
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();

            var playerIds = SteamP2PManager.Instance?.GetAllPlayerIds()?.ToList() ?? new List<int> { 0 };

            foreach (int playerId in playerIds)
            {
                string playerName = GetPlayerName(playerId);
                int dupeCount = DupeOwnership.Instance?.GetOwnedDupeCount(playerId) ?? 0;
                Color playerColor = GetPlayerColor(playerId);
                bool isSelected = _selectedPlayerId == playerId;

                var btnStyle = GetPlayerButtonStyle(playerColor, isSelected);
                if (GUILayout.Button($"{playerName}\n({dupeCount} dupes)", btnStyle, GUILayout.Height(50), GUILayout.MinWidth(100)))
                {
                    _selectedPlayerId = playerId;
                }

                GUILayout.Space(8);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawDupeList()
        {
            // Filter toggle
            GUILayout.BeginHorizontal();
            GUILayout.Label("DUPES:", GetSectionStyle());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_showUnassignedOnly ? "Show All" : "Show Unassigned", GetSmallButtonStyle()))
            {
                _showUnassignedOnly = !_showUnassignedOnly;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // Scrollable dupe list
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(220));

            var allMinions = global::Components.LiveMinionIdentities.Items;
            bool anyDupes = false;

            foreach (var minion in allMinions)
            {
                if (minion == null) continue;

                int dupeId = minion.gameObject.GetInstanceID();
                int ownerId = DupeOwnership.Instance?.GetOwnerPlayer(dupeId) ?? -1;

                // Filter if needed
                if (_showUnassignedOnly && ownerId >= 0) continue;

                anyDupes = true;
                
                // Use GetProperName() to get the actual dupe name, not GameObject.name
                string dupeName = minion.GetProperName();
                if (string.IsNullOrEmpty(dupeName))
                {
                    dupeName = minion.gameObject.name; // Fallback
                }
                
                DrawDupeEntry(dupeId, dupeName, ownerId);
            }

            if (!anyDupes)
            {
                GUILayout.Label(_showUnassignedOnly ? "All dupes are assigned!" : "No dupes found", GetDimStyle());
            }

            GUILayout.EndScrollView();
        }

        private void DrawDupeEntry(int dupeId, string dupeName, int ownerId)
        {
            bool isUnassigned = ownerId < 0;
            bool isOwnedBySelected = ownerId == _selectedPlayerId;
            Color ownerColor = isUnassigned ? UnassignedColor : GetPlayerColor(ownerId);

            // Use a button-style approach for the whole entry (works better in scroll views)
            GUILayout.BeginHorizontal();
            
            // Create the entry as a clickable box
            Texture2D bgTex = isOwnedBySelected ? _entrySelectedTex :
                              isUnassigned ? _entryUnassignedTex : _entryAssignedTex;
            
            var entryStyle = new GUIStyle(GUI.skin.box);
            entryStyle.normal.background = bgTex;
            entryStyle.hover.background = _entryHoverTex;
            entryStyle.alignment = TextAnchor.MiddleLeft;
            entryStyle.padding = new RectOffset(10, 10, 8, 8);
            
            // Build the label text
            string statusText = isUnassigned ? "(Unassigned)" : $"→ {GetPlayerName(ownerId)}";
            string fullText = $"<color=#{ColorUtility.ToHtmlStringRGB(ownerColor)}><b>{dupeName}</b></color>                    <color=#999999>{statusText}</color>";
            
            // Make the whole entry clickable
            entryStyle.richText = true;
            if (GUILayout.Button(fullText, entryStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
            {
                if (isOwnedBySelected)
                {
                    // Clicking on already-owned dupe: unassign it
                    UnassignDupe(dupeId);
                }
                else
                {
                    // Assign to selected player
                    AssignDupe(dupeId, _selectedPlayerId);
                }
            }
            
            GUILayout.EndHorizontal();
            GUILayout.Space(3);
        }

        private void DrawActions()
        {
            GUILayout.BeginHorizontal();

            // Auto-assign button
            if (GUILayout.Button("AUTO-ASSIGN", GetButtonStyle(), GUILayout.Height(36), GUILayout.Width(130)))
            {
                AutoAssignDupes();
            }

            GUILayout.Space(10);

            // Clear all button
            if (GUILayout.Button("CLEAR ALL", GetWarningButtonStyle(), GUILayout.Height(36), GUILayout.Width(100)))
            {
                ClearAllAssignments();
            }

            GUILayout.FlexibleSpace();

            // Done button
            if (GUILayout.Button("DONE", GetDoneButtonStyle(), GUILayout.Height(36), GUILayout.Width(120)))
            {
                BroadcastAssignments();
                
                // End initial spawn phase - future dupes will trigger NewDupePopup
                Patches.GamePatches.EndInitialSpawnPhase();
                
                Hide();
            }

            GUILayout.EndHorizontal();
        }

        private void AssignDupe(int dupeId, int playerId)
        {
            // Find the minion and get name for network-safe assignment
            foreach (var minion in global::Components.LiveMinionIdentities.Items)
            {
                if (minion != null && minion.gameObject.GetInstanceID() == dupeId)
                {
                    // Register the object first (ensures instance ID -> name mapping exists)
                    DupeOwnership.Instance?.RegisterDupeObject(minion.gameObject);
                    
                    // Then register ownership using name
                    string dupeName = minion.GetProperName();
                    if (!string.IsNullOrEmpty(dupeName))
                    {
                        DupeOwnership.Instance?.RegisterOwnershipByName(playerId, dupeName);
                    }
                    break;
                }
            }

            OniMultiplayerMod.Log($"[DupeAssignment] Assigned dupe {dupeId} to player {playerId}");
        }

        private void UnassignDupe(int dupeId)
        {
            DupeOwnership.Instance?.UnregisterOwnership(dupeId);
            OniMultiplayerMod.Log($"[DupeAssignment] Unassigned dupe {dupeId}");
        }

        private void AutoAssignDupes()
        {
            if (DupeOwnership.Instance == null) return;
            if (SteamP2PManager.Instance == null) return;

            var playerIds = SteamP2PManager.Instance.GetAllPlayerIds().ToList();
            var allMinions = global::Components.LiveMinionIdentities.Items.ToList();
            
            // Clear existing ownership (but keep dupe object registrations)
            DupeOwnership.Instance.ClearOwnership();

            // First, make sure all dupes are registered
            foreach (var minion in allMinions)
            {
                if (minion == null) continue;
                DupeOwnership.Instance.RegisterDupeObject(minion.gameObject);
            }

            // Distribute dupes evenly among players using NAMES
            int playerCount = playerIds.Count;
            int minionIndex = 0;

            foreach (var minion in allMinions)
            {
                if (minion == null) continue;

                int playerId = playerIds[minionIndex % playerCount];
                string dupeName = minion.GetProperName();

                if (!string.IsNullOrEmpty(dupeName))
                {
                    DupeOwnership.Instance.RegisterOwnershipByName(playerId, dupeName);
                }

                minionIndex++;
            }

            OniMultiplayerMod.Log($"[DupeAssignment] Auto-assigned {minionIndex} dupes to {playerCount} players");
        }

        private void ClearAllAssignments()
        {
            DupeOwnership.Instance?.Clear();

            // Re-register dupe objects without ownership
            foreach (var minion in global::Components.LiveMinionIdentities.Items)
            {
                if (minion != null)
                {
                    int dupeId = minion.gameObject.GetInstanceID();
                    DupeOwnership.Instance?.RegisterDupeObject(dupeId, minion.gameObject);
                }
            }

            OniMultiplayerMod.Log("[DupeAssignment] Cleared all assignments");
        }

        /// <summary>
        /// Broadcast current assignments to all clients using bulk packet.
        /// Uses dupe NAMES for network-safe identification.
        /// </summary>
        private void BroadcastAssignments()
        {
            if (DupeOwnership.Instance == null) return;
            if (SteamP2PManager.Instance == null) return;

            // Get assignments by name (network-safe)
            var assignments = DupeOwnership.Instance.GetAllAssignmentsByName();

            // Build bulk assignment list using dupe names
            var bulkAssignments = new List<DupeAssignment>();
            foreach (var kvp in assignments)
            {
                int playerId = kvp.Key;
                foreach (string dupeName in kvp.Value)
                {
                    bulkAssignments.Add(new DupeAssignment
                    {
                        PlayerId = playerId,
                        DupeName = dupeName
                    });
                }
            }

            // Send bulk assignment packet
            var bulkPacket = new BulkDupeAssignmentPacket
            {
                Assignments = bulkAssignments.ToArray()
            };
            SteamP2PManager.Instance.BroadcastToClients(bulkPacket);

            // Also send completion signal
            var completePacket = new DupeSelectionCompletePacket();
            SteamP2PManager.Instance.BroadcastToClients(completePacket);

            OniMultiplayerMod.Log($"[DupeAssignment] Broadcasted {bulkAssignments.Count} assignments to clients");
        }

        #region Styles

        private GUIStyle GetTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = AccentColor;
            return style;
        }

        private GUIStyle GetDescStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            style.normal.textColor = TextDim;
            return style;
        }

        private GUIStyle GetSectionStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = TextDim;
            return style;
        }

        private GUIStyle GetDimStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            style.normal.textColor = TextDim;
            return style;
        }

        private GUIStyle GetPlayerButtonStyle(Color color, bool isSelected)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            
            if (isSelected)
            {
                style.normal.background = MakeTex(new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 1f));
                style.normal.textColor = Color.white;
            }
            else
            {
                style.normal.background = MakeTex(new Color(0.2f, 0.2f, 0.25f, 1f));
                style.normal.textColor = color;
            }
            
            style.hover.background = MakeTex(new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 1f));
            style.hover.textColor = Color.white;
            
            return style;
        }

        private GUIStyle GetSmallButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10
            };
            style.normal.background = MakeTex(new Color(0.2f, 0.2f, 0.25f, 1f));
            style.normal.textColor = TextDim;
            return style;
        }

        private GUIStyle GetDupeNameStyle(Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = color;
            return style;
        }

        private GUIStyle GetOwnerStyle(Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight
            };
            style.normal.textColor = color;
            return style;
        }

        private GUIStyle GetButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            style.normal.background = MakeTex(new Color(0.25f, 0.25f, 0.3f, 1f));
            style.hover.background = MakeTex(new Color(0.35f, 0.35f, 0.4f, 1f));
            style.normal.textColor = TextBright;
            return style;
        }

        private GUIStyle GetWarningButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12
            };
            style.normal.background = MakeTex(new Color(0.4f, 0.25f, 0.2f, 1f));
            style.hover.background = MakeTex(new Color(0.5f, 0.3f, 0.25f, 1f));
            style.normal.textColor = new Color(0.95f, 0.7f, 0.6f, 1f);
            return style;
        }

        private GUIStyle GetDoneButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            style.normal.background = _accentTex;
            style.hover.background = MakeTex(new Color(0.4f, 0.8f, 0.6f, 1f));
            style.normal.textColor = Color.white;
            return style;
        }

        #endregion
    }
}

