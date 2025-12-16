using UnityEngine;
using OniMultiplayer.Network;
using System.Collections.Generic;
using System.Linq;

namespace OniMultiplayer.UI
{
    /// <summary>
    /// Small popup that appears when a new dupe spawns (from printing pod).
    /// Host selects which player to assign the new dupe to.
    /// Uses dupe NAMES for network-safe identification.
    /// </summary>
    public class NewDupePopup : MonoBehaviour
    {
        public static NewDupePopup Instance { get; private set; }

        private bool _isVisible = false;
        private string _pendingDupeName = "";
        private System.Action<int> _onAssigned;

        // UI colors
        private static readonly Color BgColor = new Color(0.1f, 0.1f, 0.12f, 0.98f);
        private static readonly Color AccentColor = new Color(0.35f, 0.8f, 0.55f, 1f);
        private static readonly Color Player1Color = new Color(0.4f, 0.75f, 0.95f, 1f);
        private static readonly Color Player2Color = new Color(0.95f, 0.65f, 0.4f, 1f);
        private static readonly Color Player3Color = new Color(0.8f, 0.5f, 0.9f, 1f);
        private static readonly Color TextBright = new Color(0.95f, 0.95f, 0.95f, 1f);

        private Texture2D _bgTex;
        private Texture2D _overlayTex;
        private Texture2D _borderTex;

        public static void Initialize()
        {
            if (Instance != null) return;

            var go = new GameObject("NewDupePopup");
            Instance = go.AddComponent<NewDupePopup>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            Instance = this;
            _bgTex = MakeTex(BgColor);
            _overlayTex = MakeTex(new Color(0, 0, 0, 0.5f));
            _borderTex = MakeTex(AccentColor);
        }

        private Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Show popup to assign a new dupe by NAME (network-safe). Only works for host.
        /// </summary>
        public void Show(string dupeName, System.Action<int> onAssigned = null)
        {
            if (SteamP2PManager.Instance?.IsHost != true) return;
            if (string.IsNullOrEmpty(dupeName)) return;

            _pendingDupeName = dupeName;
            _onAssigned = onAssigned;
            _isVisible = true;

            // Pause while selecting
            if (SpeedControlScreen.Instance != null && !SpeedControlScreen.Instance.IsPaused)
            {
                SpeedControlScreen.Instance.Pause(false, false);
            }

            OniMultiplayerMod.Log($"[NewDupePopup] Showing for: '{dupeName}'");
        }

        /// <summary>
        /// Hide the popup.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _pendingDupeName = "";
            
            // Unpause
            if (SpeedControlScreen.Instance != null && SpeedControlScreen.Instance.IsPaused)
            {
                SpeedControlScreen.Instance.Unpause(false);
            }
        }

        private Color GetPlayerColor(int playerId)
        {
            switch (playerId)
            {
                case 0: return Player1Color;
                case 1: return Player2Color;
                case 2: return Player3Color;
                default: return TextBright;
            }
        }

        private string GetPlayerName(int playerId)
        {
            return SteamP2PManager.Instance?.GetPlayerName(playerId) ?? $"Player {playerId}";
        }

        private void OnGUI()
        {
            if (!_isVisible) return;

            // Darken background
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlayTex);

            // Popup panel
            float popupWidth = 320;
            float popupHeight = 180;
            float x = (Screen.width - popupWidth) / 2;
            float y = (Screen.height - popupHeight) / 2;

            // Border and background
            GUI.DrawTexture(new Rect(x - 2, y - 2, popupWidth + 4, popupHeight + 4), _borderTex);
            GUI.DrawTexture(new Rect(x, y, popupWidth, popupHeight), _bgTex);

            GUILayout.BeginArea(new Rect(x + 15, y + 12, popupWidth - 30, popupHeight - 24));

            // Title
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("NEW DUPLICANT!", GetTitleStyle());
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Dupe name
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_pendingDupeName, GetDupeNameStyle());
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Instruction
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Assign to:", GetLabelStyle());
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Player buttons
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var playerIds = SteamP2PManager.Instance?.GetAllPlayerIds()?.ToList() ?? new List<int> { 0 };

            foreach (int playerId in playerIds)
            {
                string playerName = GetPlayerName(playerId);
                int dupeCount = DupeOwnership.Instance?.GetOwnedDupeCount(playerId) ?? 0;
                Color playerColor = GetPlayerColor(playerId);

                var btnStyle = GetPlayerButtonStyle(playerColor);
                if (GUILayout.Button($"{playerName}\n({dupeCount})", btnStyle, GUILayout.Width(90), GUILayout.Height(50)))
                {
                    AssignToPlayer(playerId);
                }

                GUILayout.Space(6);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void AssignToPlayer(int playerId)
        {
            if (string.IsNullOrEmpty(_pendingDupeName)) return;

            // Register ownership using dupe name (network-safe)
            DupeOwnership.Instance?.RegisterOwnershipByName(playerId, _pendingDupeName);

            // Broadcast to clients using dupe name
            var packet = new DupeAssignmentPacket
            {
                PlayerId = playerId,
                DupeName = _pendingDupeName
            };
            SteamP2PManager.Instance?.BroadcastToClients(packet);

            OniMultiplayerMod.Log($"[NewDupePopup] Assigned '{_pendingDupeName}' to {GetPlayerName(playerId)}");

            // Callback
            _onAssigned?.Invoke(playerId);

            Hide();
        }

        #region Styles

        private GUIStyle GetTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = AccentColor;
            return style;
        }

        private GUIStyle GetDupeNameStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = TextBright;
            return style;
        }

        private GUIStyle GetLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            return style;
        }

        private GUIStyle GetPlayerButtonStyle(Color color)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.background = MakeTex(new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 1f));
            style.hover.background = MakeTex(new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 1f));
            style.normal.textColor = color;
            style.hover.textColor = Color.white;
            return style;
        }

        #endregion
    }
}
