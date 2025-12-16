using UnityEngine;
using System.Collections.Generic;

namespace OniMultiplayer.UI
{
    /// <summary>
    /// In-game notification system for multiplayer events.
    /// Shows toast-style messages for errors, warnings, and info.
    /// </summary>
    public class MultiplayerNotification : MonoBehaviour
    {
        public static MultiplayerNotification Instance { get; private set; }

        // Notification types
        public enum NotificationType
        {
            Info,
            Warning,
            Error,
            Success
        }

        // Single notification data
        private class Notification
        {
            public string Message;
            public NotificationType Type;
            public float TimeRemaining;
            public float Alpha;
        }

        // Active notifications
        private readonly List<Notification> _notifications = new List<Notification>();
        private const int MaxNotifications = 5;
        private const float DefaultDuration = 5f;
        private const float FadeTime = 0.5f;

        // UI styling
        private GUIStyle _infoStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _successStyle;
        private Texture2D _bgTex;
        private bool _stylesInitialized;

        // Colors
        private static readonly Color InfoColor = new Color(0.2f, 0.4f, 0.8f, 0.9f);
        private static readonly Color WarningColor = new Color(0.9f, 0.7f, 0.1f, 0.9f);
        private static readonly Color ErrorColor = new Color(0.9f, 0.2f, 0.2f, 0.9f);
        private static readonly Color SuccessColor = new Color(0.2f, 0.8f, 0.3f, 0.9f);

        public static void Initialize()
        {
            if (Instance != null) return;

            var go = new GameObject("MultiplayerNotification");
            Instance = go.AddComponent<MultiplayerNotification>();
            DontDestroyOnLoad(go);

            OniMultiplayerMod.Log("[UI] MultiplayerNotification initialized");
        }

        private void Awake()
        {
            Instance = this;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.85f));
            _bgTex.Apply();

            _infoStyle = CreateStyle(InfoColor);
            _warningStyle = CreateStyle(WarningColor);
            _errorStyle = CreateStyle(ErrorColor);
            _successStyle = CreateStyle(SuccessColor);

            _stylesInitialized = true;
        }

        private GUIStyle CreateStyle(Color borderColor)
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                padding = new RectOffset(15, 15, 10, 10)
            };
            style.normal.background = _bgTex;
            style.normal.textColor = Color.white;
            return style;
        }

        #region Public API

        /// <summary>
        /// Show an info notification.
        /// </summary>
        public static void ShowInfo(string message, float duration = DefaultDuration)
        {
            Instance?.AddNotification(message, NotificationType.Info, duration);
        }

        /// <summary>
        /// Show a warning notification.
        /// </summary>
        public static void ShowWarning(string message, float duration = DefaultDuration)
        {
            Instance?.AddNotification(message, NotificationType.Warning, duration);
        }

        /// <summary>
        /// Show an error notification.
        /// </summary>
        public static void ShowError(string message, float duration = 8f)
        {
            Instance?.AddNotification(message, NotificationType.Error, duration);
            OniMultiplayerMod.LogError($"[Notification] {message}");
        }

        /// <summary>
        /// Show a success notification.
        /// </summary>
        public static void ShowSuccess(string message, float duration = DefaultDuration)
        {
            Instance?.AddNotification(message, NotificationType.Success, duration);
        }

        /// <summary>
        /// Clear all notifications.
        /// </summary>
        public static void ClearAll()
        {
            Instance?._notifications.Clear();
        }

        #endregion

        private void AddNotification(string message, NotificationType type, float duration)
        {
            // Remove oldest if at max
            while (_notifications.Count >= MaxNotifications)
            {
                _notifications.RemoveAt(0);
            }

            _notifications.Add(new Notification
            {
                Message = message,
                Type = type,
                TimeRemaining = duration,
                Alpha = 1f
            });

            OniMultiplayerMod.Log($"[Notification] [{type}] {message}");
        }

        private void Update()
        {
            // Update notification timers
            for (int i = _notifications.Count - 1; i >= 0; i--)
            {
                var notif = _notifications[i];
                notif.TimeRemaining -= Time.unscaledDeltaTime;

                // Fade out
                if (notif.TimeRemaining <= FadeTime)
                {
                    notif.Alpha = Mathf.Max(0, notif.TimeRemaining / FadeTime);
                }

                // Remove expired
                if (notif.TimeRemaining <= 0)
                {
                    _notifications.RemoveAt(i);
                }
            }
        }

        private void OnGUI()
        {
            if (_notifications.Count == 0) return;

            InitStyles();

            // Position in top-right corner
            float startX = Screen.width - 420;
            float startY = 80;
            float width = 400;
            float spacing = 10;

            float y = startY;

            foreach (var notif in _notifications)
            {
                // Get style and border color based on type
                GUIStyle style;
                Color borderColor;

                switch (notif.Type)
                {
                    case NotificationType.Warning:
                        style = _warningStyle;
                        borderColor = WarningColor;
                        break;
                    case NotificationType.Error:
                        style = _errorStyle;
                        borderColor = ErrorColor;
                        break;
                    case NotificationType.Success:
                        style = _successStyle;
                        borderColor = SuccessColor;
                        break;
                    default:
                        style = _infoStyle;
                        borderColor = InfoColor;
                        break;
                }

                // Apply alpha
                Color oldColor = GUI.color;
                GUI.color = new Color(1, 1, 1, notif.Alpha);

                // Calculate height based on content
                float height = style.CalcHeight(new GUIContent(notif.Message), width - 30);
                height = Mathf.Max(height, 40);

                // Draw border
                var borderRect = new Rect(startX - 2, y - 2, width + 4, height + 4);
                DrawRect(borderRect, borderColor * notif.Alpha);

                // Draw notification
                var rect = new Rect(startX, y, width, height);
                GUI.Box(rect, GetPrefix(notif.Type) + notif.Message, style);

                GUI.color = oldColor;

                y += height + spacing;
            }
        }

        private string GetPrefix(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Warning: return "⚠ ";
                case NotificationType.Error: return "✖ ";
                case NotificationType.Success: return "✓ ";
                default: return "ℹ ";
            }
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;

            // Simple border using GUI.DrawTexture
            var tex = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), tex); // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), tex); // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), tex); // Left
            GUI.DrawTexture(new Rect(rect.x + rect.width - 2, rect.y, 2, rect.height), tex); // Right

            GUI.color = oldColor;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}