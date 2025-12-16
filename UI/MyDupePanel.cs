using UnityEngine;
using OniMultiplayer.Network;
using OniMultiplayer.Components;

namespace OniMultiplayer.UI
{
    /// <summary>
    /// Compact panel showing the local player's dupe vitals.
    /// Clean, minimal design that doesn't obstruct gameplay.
    /// </summary>
    public class MyDupePanel : MonoBehaviour
    {
        public static MyDupePanel Instance { get; private set; }

        private bool _isVisible = true;
        private Rect _panelRect;
        
        // Cached dupe data
        private string _dupeName = "";
        private float _health = 100f;
        private float _maxHealth = 100f;
        private float _stress = 0f;
        private float _calories = 100f;
        private float _breath = 100f;
        private GameObject _myDupe;
        private int _myDupeId = -1;
        
        // Update throttle
        private float _updateInterval = 0.5f;
        private float _lastUpdate = 0f;

        // Compact styling
        private Texture2D _bgTex;
        private static readonly Color BgColor = new Color(0.08f, 0.08f, 0.1f, 0.85f);
        private static readonly Color AccentColor = new Color(0.35f, 0.75f, 0.5f, 1f);
        private static readonly Color WarningColor = new Color(0.95f, 0.75f, 0.25f, 1f);
        private static readonly Color DangerColor = new Color(0.95f, 0.35f, 0.35f, 1f);
        private static readonly Color BarBgColor = new Color(0.15f, 0.15f, 0.18f, 1f);

        public static void Initialize()
        {
            if (Instance != null) return;

            var go = new GameObject("MyDupePanel");
            Instance = go.AddComponent<MyDupePanel>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            Instance = this;
            _bgTex = MakeTex(BgColor);
            _panelRect = new Rect(8, 8, 160, 90);
        }

        private Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void Update()
        {
            if (!_isVisible) return;
            if (!IsMultiplayer()) return;

            if (Time.unscaledTime - _lastUpdate < _updateInterval) return;
            _lastUpdate = Time.unscaledTime;

            UpdateDupeData();
        }

        private void UpdateDupeData()
        {
            int dupeId = DupeOwnership.Instance?.GetLocalPlayerDupe() ?? -1;
            
            if (dupeId < 0)
            {
                _dupeName = "";
                _myDupe = null;
                _myDupeId = -1;
                return;
            }

            if (_myDupe == null || _myDupeId != dupeId)
            {
                _myDupe = DupeOwnership.Instance?.GetDupeObject(dupeId);
                _myDupeId = dupeId;
                
                if (_myDupe == null)
                {
                    _myDupe = FindDupeById(dupeId);
                }
            }

            if (_myDupe == null) return;

            var identity = _myDupe.GetComponent<MinionIdentity>();
            if (identity != null) _dupeName = identity.name;

            var health = _myDupe.GetComponent<Health>();
            if (health != null)
            {
                _health = health.hitPoints;
                _maxHealth = health.maxHitPoints;
            }

            try { var s = Db.Get().Amounts.Stress.Lookup(_myDupe); if (s != null) _stress = s.value; } catch { }
            try { var c = Db.Get().Amounts.Calories.Lookup(_myDupe); if (c != null) _calories = (c.value / c.GetMax()) * 100f; } catch { }
            try { var b = Db.Get().Amounts.Breath.Lookup(_myDupe); if (b != null) _breath = (b.value / b.GetMax()) * 100f; } catch { }
        }

        private GameObject FindDupeById(int instanceId)
        {
            foreach (var minion in global::Components.LiveMinionIdentities.Items)
            {
                if (minion != null && minion.gameObject.GetInstanceID() == instanceId)
                    return minion.gameObject;
            }
            return null;
        }

        private void OnGUI()
        {
            if (!_isVisible) return;
            if (!IsMultiplayer()) return;
            if (Game.Instance == null) return;
            if (string.IsNullOrEmpty(_dupeName)) return;

            // Background
            GUI.DrawTexture(_panelRect, _bgTex);

            GUILayout.BeginArea(new Rect(_panelRect.x + 8, _panelRect.y + 6, _panelRect.width - 16, _panelRect.height - 12));

            // Name with star
            GUILayout.Label($"★ {_dupeName}", GetNameStyle());
            
            GUILayout.Space(4);

            // Compact vital bars
            DrawMiniBar("HP", _health / _maxHealth, GetVitalColor(_health / _maxHealth));
            DrawMiniBar("STR", 1f - (_stress / 100f), GetStressColor(_stress));
            DrawMiniBar("CAL", _calories / 100f, GetVitalColor(_calories / 100f));
            DrawMiniBar("O2", _breath / 100f, GetVitalColor(_breath / 100f));

            GUILayout.EndArea();

            // Follow button (small, in corner)
            DrawFollowButton();
        }

        private void DrawMiniBar(string label, float percent, Color color)
        {
            GUILayout.BeginHorizontal();
            
            // Label
            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 9 };
            labelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.65f);
            GUILayout.Label(label, labelStyle, GUILayout.Width(24));
            
            // Bar
            Rect barRect = GUILayoutUtility.GetRect(100, 10, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(barRect, MakeTex(BarBgColor));
            
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(percent), barRect.height);
            GUI.DrawTexture(fillRect, MakeTex(color));
            
            GUILayout.EndHorizontal();
        }

        private void DrawFollowButton()
        {
            bool isFollowing = DupeCameraFollow.Instance?.IsFollowing ?? false;
            
            Rect btnRect = new Rect(_panelRect.x + _panelRect.width - 22, _panelRect.y + _panelRect.height - 22, 18, 18);
            
            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 10, padding = new RectOffset(0,0,0,0) };
            
            if (GUI.Button(btnRect, isFollowing ? "◉" : "○", btnStyle))
            {
                DupeCameraFollow.Instance?.ToggleFollow();
            }
        }

        private Color GetVitalColor(float percent)
        {
            if (percent > 0.5f) return AccentColor;
            if (percent > 0.25f) return WarningColor;
            return DangerColor;
        }

        private Color GetStressColor(float stress)
        {
            if (stress < 40f) return AccentColor;
            if (stress < 70f) return WarningColor;
            return DangerColor;
        }

        private bool IsMultiplayer()
        {
            return SteamP2PManager.Instance?.IsConnected == true || 
                   SteamP2PManager.Instance?.IsHost == true;
        }

        private GUIStyle GetNameStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = AccentColor;
            return style;
        }

        public void Show() => _isVisible = true;
        public void Hide() => _isVisible = false;
        public void Toggle() => _isVisible = !_isVisible;
    }
}
