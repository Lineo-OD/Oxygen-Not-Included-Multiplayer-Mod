using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using OniMultiplayer.Network;
using System.Collections;
using System.Collections.Generic;

namespace OniMultiplayer.UI
{
    /// <summary>
    /// Full-screen multiplayer lobby that looks native to ONI.
    /// Replaces the main menu when opened, like "New Game" or "Load Game" screens.
    /// </summary>
    public class MultiplayerScreen : KScreen
    {
        public static MultiplayerScreen Instance { get; private set; }

        // Screen state
        private enum ScreenState { Menu, InLobby, GameModeSelect }
        private ScreenState _state = ScreenState.Menu;
        
        // Game mode selection
        private bool _startNewGame = false; // true = new game, false = load game
        private bool _isStartingGame = false; // Prevent double-start
        
        // Deduplication flags to prevent multiple event processing
        private bool _hasRegisteredEvents = false;
        private CSteamID _lastJoinedLobby = CSteamID.Nil;
        private bool _gameStartEventProcessed = false;

        // Lobby data
        private string _lobbyCode = "";
        private string _joinCodeInput = "";
        private List<LobbyPlayer> _players = new List<LobbyPlayer>();
        private string _statusMessage = "";

        // MP Save selection
        private string _selectedMPSave = "";
        private List<Systems.MultiplayerSaveManager.MPSaveInfo> _mpSaves = new List<Systems.MultiplayerSaveManager.MPSaveInfo>();
        private List<Systems.MultiplayerSaveManager.MPSaveInfo> _spSavesForImport = new List<Systems.MultiplayerSaveManager.MPSaveInfo>();
        private bool _showImportPanel = false;
        private Vector2 _saveListScroll = Vector2.zero;
        private Vector2 _importListScroll = Vector2.zero;

        // UI colors matching ONI style
        private static readonly Color BgColor = new Color(0.12f, 0.12f, 0.16f, 1f);
        private static readonly Color PanelColor = new Color(0.18f, 0.18f, 0.22f, 1f);
        private static readonly Color AccentColor = new Color(0.3f, 0.6f, 0.9f, 1f);
        private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.32f, 1f);
        private static readonly Color ButtonHoverColor = new Color(0.35f, 0.35f, 0.45f, 1f);
        private static readonly Color SuccessColor = new Color(0.3f, 0.8f, 0.4f, 1f);
        private static readonly Color WarningColor = new Color(0.9f, 0.7f, 0.2f, 1f);

        // Textures
        private Texture2D _bgTex;
        private Texture2D _panelTex;
        private Texture2D _buttonTex;
        private Texture2D _buttonHoverTex;
        private Texture2D _accentTex;

        public override float GetSortKey() => 50f;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Instance = this;
            
            // Subscribe to Steam events - only once!
            RegisterEvents();
            
            CreateTextures();
        }
        
        /// <summary>
        /// Register event handlers only once to prevent duplicates.
        /// </summary>
        private void RegisterEvents()
        {
            if (_hasRegisteredEvents) return;
            
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.OnLobbyCreated += OnLobbyCreated;
                SteamLobbyManager.Instance.OnLobbyJoined += OnLobbyJoined;
                SteamLobbyManager.Instance.OnLobbyLeft += OnLobbyLeft;
                SteamLobbyManager.Instance.OnPlayerJoinedLobby += OnPlayerJoined;
                SteamLobbyManager.Instance.OnPlayerLeftLobby += OnPlayerLeft;
                SteamLobbyManager.Instance.OnGameStartRequested += OnGameStart;
                _hasRegisteredEvents = true;
            }
        }
        
        /// <summary>
        /// Unregister event handlers to prevent memory leaks.
        /// </summary>
        private void UnregisterEvents()
        {
            if (!_hasRegisteredEvents) return;
            
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.OnLobbyCreated -= OnLobbyCreated;
                SteamLobbyManager.Instance.OnLobbyJoined -= OnLobbyJoined;
                SteamLobbyManager.Instance.OnLobbyLeft -= OnLobbyLeft;
                SteamLobbyManager.Instance.OnPlayerJoinedLobby -= OnPlayerJoined;
                SteamLobbyManager.Instance.OnPlayerLeftLobby -= OnPlayerLeft;
                SteamLobbyManager.Instance.OnGameStartRequested -= OnGameStart;
                _hasRegisteredEvents = false;
            }
        }
        
        protected override void OnCleanUp()
        {
            base.OnCleanUp();
            UnregisterEvents();
            if (Instance == this) Instance = null;
        }

        private void CreateTextures()
        {
            _bgTex = MakeTex(BgColor);
            _panelTex = MakeTex(PanelColor);
            _buttonTex = MakeTex(ButtonColor);
            _buttonHoverTex = MakeTex(ButtonHoverColor);
            _accentTex = MakeTex(AccentColor);
        }

        private Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            
            // Ensure events are registered (in case OnPrefabInit wasn't called or events were cleared)
            RegisterEvents();
            
            // Reset state when screen opens
            _isStartingGame = false;
            _gameStartEventProcessed = false;
            _selectedMPSave = ""; // Clear save selection - require explicit choice
            _mpSaves.Clear(); // Will be refreshed when needed
            
            // Check if already in a lobby
            if (SteamLobbyManager.Instance?.IsInLobby ?? false)
            {
                _lobbyCode = SteamMatchmaking.GetLobbyData(SteamLobbyManager.Instance.CurrentLobby, "lobby_code");
                if (string.IsNullOrEmpty(_lobbyCode))
                {
                    _lobbyCode = GenerateLobbyCode(SteamLobbyManager.Instance.CurrentLobby);
                }
                RefreshPlayers();
                _state = ScreenState.InLobby;
                _lastJoinedLobby = SteamLobbyManager.Instance.CurrentLobby;
            }
            else
            {
                _state = ScreenState.Menu;
                _lastJoinedLobby = CSteamID.Nil;
            }
            
            // Hide main menu
            if (MainMenu.Instance != null)
            {
                MainMenu.Instance.gameObject.SetActive(false);
            }
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
            
            // Show main menu again
            if (MainMenu.Instance != null)
            {
                MainMenu.Instance.gameObject.SetActive(true);
            }
        }

        private void Update()
        {
            // ESC to go back
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_state == ScreenState.InLobby)
                {
                    // Leave lobby first
                    SteamLobbyManager.Instance?.LeaveLobby();
                }
                else
                {
                    // Close screen
                    Deactivate();
                }
            }
        }

        private void OnGUI()
        {
            if (!isActiveAndEnabled) return;

            // Full screen dark background
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTex);

            // Center content area - taller for game mode select screen
            float contentWidth = 600;
            float contentHeight = _state == ScreenState.GameModeSelect ? 620 : 550;
            float x = (Screen.width - contentWidth) / 2;
            float y = (Screen.height - contentHeight) / 2;

            // Main panel
            var panelRect = new Rect(x, y, contentWidth, contentHeight);
            GUI.DrawTexture(panelRect, _panelTex);

            // Draw border
            DrawBorder(panelRect, AccentColor, 2);

            GUILayout.BeginArea(new Rect(x + 30, y + 20, contentWidth - 60, contentHeight - 40));

            if (_state == ScreenState.Menu)
            {
                DrawMenuScreen();
            }
            else if (_state == ScreenState.GameModeSelect)
            {
                DrawGameModeSelectScreen();
            }
            else
            {
                DrawLobbyScreen();
            }

            GUILayout.EndArea();

            // Back button (bottom left, outside panel)
            if (GUI.Button(new Rect(x, y + contentHeight + 15, 100, 35), "← BACK", GetButtonStyle()))
            {
                if (_state == ScreenState.InLobby)
                {
                    SteamLobbyManager.Instance?.LeaveLobby();
                }
                else if (_state == ScreenState.GameModeSelect)
                {
                    _state = ScreenState.InLobby;
                    return;
                }
                Deactivate();
            }
        }

        private void DrawMenuScreen()
        {
            // Title
            GUILayout.Label("MULTIPLAYER", GetTitleStyle());
            GUILayout.Space(10);
            
            // Steam status
            if (SteamManager.Initialized)
            {
                GUILayout.Label($"Steam: {SteamFriends.GetPersonaName()}", GetSubtitleStyle());
            }
            
            GUILayout.Space(30);

            // === HOST SECTION ===
            GUILayout.Label("HOST A GAME", GetSectionStyle());
            GUILayout.Space(10);
            
            GUILayout.Label("Create a lobby and share the code with friends.", GetDescStyle());
            GUILayout.Space(10);

            if (GUILayout.Button("CREATE LOBBY", GetButtonStyle(), GUILayout.Height(50)))
            {
                _statusMessage = "Creating lobby...";
                string worldName = SaveGame.Instance?.BaseName ?? "Colony";
                SteamLobbyManager.Instance?.CreateLobby(worldName, true);
            }

            GUILayout.Space(40);

            // === JOIN SECTION ===
            GUILayout.Label("JOIN A GAME", GetSectionStyle());
            GUILayout.Space(10);
            
            GUILayout.Label("Enter the 6-character code from your friend:", GetDescStyle());
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            
            // Code input field - large and centered
            var inputStyle = GetInputStyle();
            _joinCodeInput = GUILayout.TextField(_joinCodeInput.ToUpper(), 6, inputStyle, GUILayout.Width(200), GUILayout.Height(50));
            _joinCodeInput = _joinCodeInput.ToUpper();
            
            GUILayout.Space(20);
            
            GUI.enabled = _joinCodeInput.Length == 6;
            if (GUILayout.Button("JOIN", GetButtonStyle(), GUILayout.Width(120), GUILayout.Height(50)))
            {
                _statusMessage = "Searching for lobby...";
                FindLobbyByCode(_joinCodeInput);
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Label(_statusMessage, GetStatusStyle());
            }
        }

        private void DrawLobbyScreen()
        {
            // Title with lobby code
            GUILayout.Label("LOBBY", GetTitleStyle());
            GUILayout.Space(15);

            // Big lobby code display
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUILayout.BeginVertical("box");
            GUILayout.Label("LOBBY CODE", GetLabelStyle());
            GUILayout.Label(_lobbyCode, GetCodeStyle());
            GUILayout.Label("Share this with friends!", GetDescStyle());
            GUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            // Players list
            GUILayout.Label($"PLAYERS ({_players.Count})", GetSectionStyle());
            GUILayout.Space(5);

            // Player list in a scrollable box
            GUILayout.BeginVertical("box", GUILayout.Height(100));
            foreach (var player in _players)
            {
                GUILayout.BeginHorizontal();
                
                string icon = player.IsHost ? "★ " : "   ";
                string you = (player.SteamId == SteamUser.GetSteamID()) ? " (You)" : "";
                
                var playerStyle = GetPlayerStyle(player.IsHost);
                GUILayout.Label($"{icon}{player.Name}{you}", playerStyle);
                
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.Space(15);

            // Start game button (host only) - goes to game mode selection
            bool isHost = SteamLobbyManager.Instance?.IsLobbyOwner ?? false;
            
            if (isHost)
            {
                if (GUILayout.Button("START GAME", GetStartButtonStyle(), GUILayout.Height(45)))
                {
                    _state = ScreenState.GameModeSelect;
                }
            }
            else
            {
                GUILayout.Label("Waiting for host to start...", GetWaitingStyle());
            }

            GUILayout.Space(10);

            // Action buttons row
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("INVITE", GetButtonStyle(), GUILayout.Height(35)))
            {
                SteamLobbyManager.Instance?.OpenSteamInviteOverlay();
            }
            
            GUILayout.Space(15);
            
            if (GUILayout.Button("LEAVE", GetButtonStyle(true), GUILayout.Height(35)))
            {
                SteamLobbyManager.Instance?.LeaveLobby();
            }
            
            GUILayout.EndHorizontal();
        }

        private void DrawGameModeSelectScreen()
        {
            // Refresh saves list if empty
            if (_mpSaves.Count == 0)
            {
                RefreshMPSaves();
            }

            // Title
            GUILayout.Label("SELECT GAME MODE", GetTitleStyle());
            GUILayout.Space(20);

            // New Game option
            GUILayout.BeginVertical("box");
            GUILayout.Space(8);
            GUILayout.Label("NEW GAME", GetSectionStyle());
            GUILayout.Label("Start a fresh colony. All players will choose their dupe during character selection.", GetDescStyle());
            GUILayout.Space(8);
            if (GUILayout.Button("START NEW GAME", GetStartButtonStyle(), GUILayout.Height(40)))
            {
                // Only set flag if we're not already starting
                if (!_isStartingGame)
                {
                    _startNewGame = true;
                    StartMultiplayerGame();
                }
                else
                {
                    OniMultiplayerMod.Log("Game start already in progress, ignoring New Game click");
                }
            }
            GUILayout.Space(8);
            GUILayout.EndVertical();

            GUILayout.Space(15);

            // Load Game option - MP saves only
            GUILayout.BeginVertical("box");
            GUILayout.Space(8);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("MULTIPLAYER SAVES", GetSectionStyle());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("REFRESH", GetSmallButtonStyle(), GUILayout.Width(70), GUILayout.Height(22)))
            {
                RefreshMPSaves();
            }
            if (GUILayout.Button("IMPORT", GetSmallButtonStyle(), GUILayout.Width(70), GUILayout.Height(22)))
            {
                _showImportPanel = !_showImportPanel;
                if (_showImportPanel)
                {
                    RefreshSPSaves();
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);

            if (_showImportPanel)
            {
                DrawImportPanel();
            }
            else if (_mpSaves.Count > 0)
            {
                // Show selection hint if nothing selected
                if (string.IsNullOrEmpty(_selectedMPSave))
                {
                    GUILayout.Label("⚠ Click SELECT on a save below to choose it:", GetDescStyle());
                    GUILayout.Space(3);
                }
                DrawMPSavesList();
            }
            else
            {
                GUILayout.Label("No multiplayer saves found.", GetDescStyle());
                GUILayout.Label("Click IMPORT to copy a single-player save to multiplayer.", GetDescStyle());
                
                string mpFolder = Systems.MultiplayerSaveManager.Instance?.GetMPSaveFolder() ?? "";
                if (!string.IsNullOrEmpty(mpFolder))
                {
                    GUILayout.Label($"MP saves folder: {mpFolder}", GetSmallDescStyle());
                }
            }
            
            GUILayout.Space(8);
            
            // Load button - show different text based on selection
            bool hasValidSave = !string.IsNullOrEmpty(_selectedMPSave) && System.IO.File.Exists(_selectedMPSave);
            
            // Show selected save info
            if (hasValidSave)
            {
                string saveName = System.IO.Path.GetFileNameWithoutExtension(_selectedMPSave);
                GUILayout.Label($"Selected: {saveName}", GetDescStyle());
            }
            
            GUI.enabled = hasValidSave;
            
            string loadButtonText = hasValidSave 
                ? "► LOAD SELECTED SAVE"
                : "(Select a save above first)";
                
            if (GUILayout.Button(loadButtonText, hasValidSave ? GetStartButtonStyle() : GetButtonStyle(), GUILayout.Height(40)))
            {
                // Only set flag if we're not already starting
                if (!_isStartingGame)
                {
                    OniMultiplayerMod.Log($"[UI] Load button clicked! Save: {_selectedMPSave}");
                    _startNewGame = false;
                    StartMultiplayerGame();
                }
                else
                {
                    OniMultiplayerMod.Log("Game start already in progress, ignoring Load click");
                }
            }
            GUI.enabled = true;
            
            GUILayout.Space(8);
            GUILayout.EndVertical();
            
            // Note: Back button is outside the panel (see OnGUI) and handles going back to lobby
        }

        private void DrawMPSavesList()
        {
            _saveListScroll = GUILayout.BeginScrollView(_saveListScroll, GUILayout.Height(150));
            
            foreach (var save in _mpSaves)
            {
                bool isSelected = _selectedMPSave == save.FullPath;
                
                GUILayout.BeginHorizontal(isSelected ? "box" : GUIStyle.none);
                
                // Selection indicator
                if (isSelected)
                {
                    GUILayout.Label("►", GetDescStyle(), GUILayout.Width(15));
                }
                else
                {
                    GUILayout.Space(15);
                }
                
                // Save info
                GUILayout.BeginVertical();
                
                string displayName = save.Metadata?.SaveName ?? System.IO.Path.GetFileNameWithoutExtension(save.FileName);
                GUILayout.Label(displayName, GetSaveNameStyle(isSelected));
                
                string sizeStr = FormatFileSize(save.FileSizeBytes);
                string dateStr = save.LastModified.ToString("MMM dd, yyyy HH:mm");
                string hashStr = !string.IsNullOrEmpty(save.Hash) ? $" | Hash: {save.Hash.Substring(0, 8)}..." : "";
                GUILayout.Label($"{dateStr} | {sizeStr}{hashStr}", GetSmallDescStyle());
                
                GUILayout.EndVertical();
                
                GUILayout.FlexibleSpace();
                
                // Select button
                if (!isSelected)
                {
                    if (GUILayout.Button("SELECT", GetSmallButtonStyle(), GUILayout.Width(60), GUILayout.Height(22)))
                    {
                        _selectedMPSave = save.FullPath;
                    }
                }
                
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
            
            GUILayout.EndScrollView();
        }

        private void DrawImportPanel()
        {
            GUILayout.Label("Import from Single-Player:", GetDescStyle());
            GUILayout.Space(5);
            
            _importListScroll = GUILayout.BeginScrollView(_importListScroll, GUILayout.Height(130));
            
            if (_spSavesForImport.Count == 0)
            {
                GUILayout.Label("No single-player saves found.", GetDescStyle());
            }
            else
            {
                foreach (var save in _spSavesForImport)
                {
                    GUILayout.BeginHorizontal();
                    
                    string displayName = System.IO.Path.GetFileNameWithoutExtension(save.FileName);
                    string sizeStr = FormatFileSize(save.FileSizeBytes);
                    GUILayout.Label($"{displayName} ({sizeStr})", GetDescStyle());
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("IMPORT", GetSmallButtonStyle(), GUILayout.Width(60), GUILayout.Height(20)))
                    {
                        if (Systems.MultiplayerSaveManager.Instance?.ImportFromSinglePlayer(save.FullPath) == true)
                        {
                            _statusMessage = $"Imported: {displayName}";
                            RefreshMPSaves();
                            _showImportPanel = false;
                        }
                        else
                        {
                            _statusMessage = "Import failed!";
                        }
                    }
                    
                    GUILayout.EndHorizontal();
                }
            }
            
            GUILayout.EndScrollView();
            
            if (GUILayout.Button("CANCEL", GetSmallButtonStyle(), GUILayout.Height(22)))
            {
                _showImportPanel = false;
            }
        }

        private void RefreshMPSaves()
        {
            _mpSaves = Systems.MultiplayerSaveManager.Instance?.GetMPSaves() ?? new List<Systems.MultiplayerSaveManager.MPSaveInfo>();
            
            // Don't auto-select - require explicit user selection
            // Only keep selection if it's still valid
            if (!string.IsNullOrEmpty(_selectedMPSave))
            {
                bool stillExists = _mpSaves.Exists(s => s.FullPath == _selectedMPSave);
                if (!stillExists)
                {
                    _selectedMPSave = ""; // Clear invalid selection
                }
            }
        }

        private void RefreshSPSaves()
        {
            _spSavesForImport = Systems.MultiplayerSaveManager.Instance?.GetSinglePlayerSaves() ?? new List<Systems.MultiplayerSaveManager.MPSaveInfo>();
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes >= 1048576) return $"{bytes / 1048576.0:F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F0} KB";
            return $"{bytes} B";
        }

        private void StartMultiplayerGame()
        {
            // Prevent double-starts
            if (_isStartingGame)
            {
                OniMultiplayerMod.Log("Game start already in progress, ignoring");
                return;
            }
            _isStartingGame = true;
            
            OniMultiplayerMod.Log($"Starting multiplayer game. New game: {_startNewGame}");
            
            // Signal lobby to start
            SteamLobbyManager.Instance?.StartGame();
        }

        #region Styles

        private GUIStyle GetTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private GUIStyle GetSubtitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = SuccessColor;
            return style;
        }

        private GUIStyle GetSectionStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = AccentColor;
            return style;
        }

        private GUIStyle GetDescStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13
            };
            style.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            return style;
        }

        private GUIStyle GetLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            return style;
        }

        private GUIStyle GetCodeStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = SuccessColor;
            return style;
        }

        private GUIStyle GetInputStyle()
        {
            var style = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.background = _buttonTex;
            style.focused.background = _buttonHoverTex;
            style.normal.textColor = Color.white;
            style.focused.textColor = Color.white;
            return style;
        }

        private GUIStyle GetButtonStyle(bool isWarning = false)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            style.normal.background = _buttonTex;
            style.hover.background = _buttonHoverTex;
            style.active.background = _buttonHoverTex;
            style.normal.textColor = isWarning ? WarningColor : Color.white;
            style.hover.textColor = isWarning ? WarningColor : Color.white;
            return style;
        }

        private GUIStyle GetStartButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            style.normal.background = _accentTex;
            style.hover.background = _buttonHoverTex;
            style.normal.textColor = Color.white;
            return style;
        }

        private GUIStyle GetPlayerStyle(bool isHost)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16
            };
            style.normal.textColor = isHost ? WarningColor : Color.white;
            return style;
        }

        private GUIStyle GetStatusStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            return style;
        }

        private GUIStyle GetWaitingStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = WarningColor;
            return style;
        }

        private GUIStyle GetSmallButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal
            };
            style.normal.background = _buttonTex;
            style.hover.background = _buttonHoverTex;
            style.active.background = _buttonHoverTex;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            return style;
        }

        private GUIStyle GetSmallDescStyle()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10
            };
            style.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            return style;
        }

        private GUIStyle GetSaveNameStyle(bool isSelected)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
            };
            style.normal.textColor = isSelected ? AccentColor : Color.white;
            return style;
        }

        #endregion

        #region Helpers

        private void DrawBorder(Rect rect, Color color, int thickness)
        {
            var borderTex = MakeTex(color);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), borderTex); // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), borderTex); // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), borderTex); // Left
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), borderTex); // Right
        }

        private string GenerateLobbyCode(CSteamID lobbyId)
        {
            ulong id = lobbyId.m_SteamID;
            string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            
            char[] code = new char[6];
            for (int i = 0; i < 6; i++)
            {
                code[i] = chars[(int)(id % (ulong)chars.Length)];
                id /= (ulong)chars.Length;
            }
            
            return new string(code);
        }

        private void FindLobbyByCode(string code)
        {
            OniMultiplayerMod.Log($"Searching for lobby with code: {code}");
            
            SteamMatchmaking.AddRequestLobbyListStringFilter("lobby_code", code.ToUpper(), ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);
            SteamMatchmaking.RequestLobbyList();
            
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.OnLobbyListReceived += OnCodeSearchResult;
            }
        }

        private void OnCodeSearchResult(List<LobbyInfo> lobbies)
        {
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.OnLobbyListReceived -= OnCodeSearchResult;
            }

            if (lobbies.Count > 0)
            {
                _statusMessage = "Found lobby, joining...";
                SteamLobbyManager.Instance?.JoinLobby(lobbies[0].LobbyId);
            }
            else
            {
                _statusMessage = "No lobby found with that code!";
            }
        }

        private void RefreshPlayers()
        {
            _players = SteamLobbyManager.Instance?.GetLobbyPlayers() ?? new List<LobbyPlayer>();
        }

        #endregion

        #region Events

        private void OnLobbyCreated(CSteamID lobbyId)
        {
            _lobbyCode = GenerateLobbyCode(lobbyId);
            SteamMatchmaking.SetLobbyData(lobbyId, "lobby_code", _lobbyCode);
            
            _statusMessage = "";
            RefreshPlayers();
            _state = ScreenState.InLobby;
            
            OniMultiplayerMod.Log($"Lobby created with code: {_lobbyCode}");
        }

        private void OnLobbyJoined(CSteamID lobbyId)
        {
            // Prevent duplicate processing of the same lobby join
            if (_lastJoinedLobby == lobbyId)
            {
                return; // Already processed this lobby join
            }
            _lastJoinedLobby = lobbyId;
            
            _lobbyCode = SteamMatchmaking.GetLobbyData(lobbyId, "lobby_code");
            if (string.IsNullOrEmpty(_lobbyCode))
            {
                _lobbyCode = GenerateLobbyCode(lobbyId);
            }
            
            _statusMessage = "";
            _gameStartEventProcessed = false; // Reset game start flag for new lobby
            RefreshPlayers();
            _state = ScreenState.InLobby;
            
            OniMultiplayerMod.Log($"Joined lobby with code: {_lobbyCode}");
        }

        private void OnLobbyLeft()
        {
            _lobbyCode = "";
            _players.Clear();
            _state = ScreenState.Menu;
            _isStartingGame = false; // Reset on lobby leave
            _gameStartEventProcessed = false; // Reset game start flag
            _lastJoinedLobby = CSteamID.Nil; // Clear last joined lobby
            _selectedMPSave = ""; // Clear save selection
            _startNewGame = false;
            
            // Reset lobby manager state
            SteamLobbyManager.Instance?.ResetGameState();
            
            // Reset ClientMode - no longer in multiplayer
            Systems.ClientMode.Leave();
        }

        private void OnPlayerJoined(CSteamID steamId, string name)
        {
            RefreshPlayers();
        }

        private void OnPlayerLeft(CSteamID steamId)
        {
            RefreshPlayers();
        }

        private void OnGameStart(CSteamID lobbyId)
        {
            OniMultiplayerMod.Log($"[GameStart] OnGameStart called! lobbyId={lobbyId}, _gameStartEventProcessed={_gameStartEventProcessed}, _isStartingGame={_isStartingGame}, _startNewGame={_startNewGame}");
            
            // Prevent duplicate game start events - this is critical!
            if (_gameStartEventProcessed)
            {
                OniMultiplayerMod.Log("[GameStart] Already processed, ignoring duplicate event");
                return;
            }
            _gameStartEventProcessed = true;
            
            // Log context for debugging
            if (!_isStartingGame)
            {
                OniMultiplayerMod.Log("[GameStart] Called but _isStartingGame is false - this is a client receiving the event from host");
            }
            
            OniMultiplayerMod.Log("[GameStart] Processing game start event...");
            
            bool isHost = SteamLobbyManager.Instance?.IsLobbyOwner ?? false;
            OniMultiplayerMod.Log($"[GameStart] isHost={isHost}");
            
            // Set ClientMode BEFORE anything else - this determines simulation behavior
            if (isHost)
            {
                Systems.ClientMode.EnterAsHost();
            }
            else
            {
                Systems.ClientMode.EnterAsClient();
            }
            
            // Initialize P2P networking
            if (isHost)
            {
                OniMultiplayerMod.Log("Started as host");
                SteamP2PManager.Instance?.StartHost();
                
                // Get expected client count (lobby members minus host)
                int expectedClients = SteamMatchmaking.GetNumLobbyMembers(lobbyId) - 1;
                
                if (expectedClients > 0)
                {
                    // Wait for clients to connect before starting
                    OniMultiplayerMod.Log($"[Host] Waiting for {expectedClients} client(s) to connect...");
                    
                    // Validate MonoBehaviour before starting coroutine
                    if (this != null && gameObject != null && gameObject.activeInHierarchy)
                    {
                        StartCoroutine(WaitForClientsAndStart(expectedClients));
                    }
                    else
                    {
                        OniMultiplayerMod.LogWarning("[Host] MultiplayerScreen not active, executing game start immediately");
                        ExecuteGameStart();
                    }
                }
                else
                {
                    // No clients, start immediately (solo host)
                    ExecuteGameStart();
                }
            }
            else
            {
                var hostId = SteamMatchmaking.GetLobbyOwner(lobbyId);
                SteamP2PManager.Instance?.ConnectToHost(hostId);
                
                // Client will receive GameStartPacket and load the save, 
                // or NewGameStartPacket to go to character selection
                OniMultiplayerMod.Log("Waiting for host to send game info...");
                _statusMessage = "Waiting for host...";
                
                // Close this screen
                Deactivate();
            }
        }
        
        /// <summary>
        /// Wait for all expected clients to connect via P2P before starting the game.
        /// </summary>
        private IEnumerator WaitForClientsAndStart(int expectedClients)
        {
            float timeout = 30f; // 30 second timeout
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                int connectedClients = (SteamP2PManager.Instance?.GetPlayerCount() ?? 1) - 1; // Minus host
                
                if (connectedClients >= expectedClients)
                {
                    OniMultiplayerMod.Log($"[Host] All {connectedClients} client(s) connected!");
                    break;
                }
                
                // Update status
                _statusMessage = $"Waiting for players... ({connectedClients}/{expectedClients})";
                
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }
            
            if (elapsed >= timeout)
            {
                int connected = (SteamP2PManager.Instance?.GetPlayerCount() ?? 1) - 1;
                OniMultiplayerMod.LogWarning($"[Host] Timeout waiting for clients. Starting with {connected}/{expectedClients} connected.");
            }
            
            // Now execute the game start
            ExecuteGameStart();
            
            // Close this screen after clients are connected
            Deactivate();
        }
        
        /// <summary>
        /// Actually start the game (called after clients connect).
        /// </summary>
        private void ExecuteGameStart()
        {
            OniMultiplayerMod.Log($"[GameStart] ExecuteGameStart called! _startNewGame={_startNewGame}");
            
            if (_startNewGame)
            {
                OniMultiplayerMod.Log("[GameStart] Calling StartNewGameAsHost()");
                StartNewGameAsHost();
            }
            else
            {
                OniMultiplayerMod.Log("[GameStart] Calling LoadGameAsHost()");
                LoadGameAsHost();
            }
        }
        
        /// <summary>
        /// Host: Start a new game - go to world selection.
        /// </summary>
        private void StartNewGameAsHost()
        {
            OniMultiplayerMod.Log("[Host] StartNewGameAsHost called!");
            OniMultiplayerMod.Log("[Host] Starting new game - going to world selection");
            
            // Send packet to clients telling them a new game is starting
            OniMultiplayerMod.Log("[Host] Sending NewGameStartPacket to all clients...");
            var newGamePacket = new NewGameStartPacket();
            SteamP2PManager.Instance?.BroadcastToClients(newGamePacket);
            OniMultiplayerMod.Log("[Host] NewGameStartPacket sent!");
            
            // Re-enable MainMenu before calling NewGame (we disabled it)
            if (MainMenu.Instance != null)
            {
                MainMenu.Instance.gameObject.SetActive(true);
                
                var newGameMethod = typeof(MainMenu).GetMethod("NewGame", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    
                if (newGameMethod != null)
                {
                    // Deactivate our screen first
                    Deactivate();
                    
                    newGameMethod.Invoke(MainMenu.Instance, null);
                    OniMultiplayerMod.Log("[Host] NewGame() called via reflection");
                }
                else
                {
                    OniMultiplayerMod.LogError("[Host] Could not find MainMenu.NewGame method!");
                    _statusMessage = "Error: Could not start new game";
                    _isStartingGame = false;
                }
            }
            else
            {
                OniMultiplayerMod.LogError("[Host] MainMenu.Instance is null - may need to return to main menu first!");
                _statusMessage = "Error: Return to main menu first";
                _isStartingGame = false;
            }
        }
        
        /// <summary>
        /// Host: Load a save and notify clients.
        /// </summary>
        private void LoadGameAsHost()
        {
            LoadGameAsHost(_selectedMPSave);
        }

        /// <summary>
        /// Host: Load a specific save and notify clients.
        /// </summary>
        private void LoadGameAsHost(string savePath)
        {
            // If no path specified, try the selected MP save
            if (string.IsNullOrEmpty(savePath))
            {
                savePath = _selectedMPSave;
            }
            
            OniMultiplayerMod.Log($"[Host] LoadGameAsHost called. Path: '{savePath ?? "null"}'");
            
            // Verify we have a valid MP save (no fallback to SP saves!)
            if (string.IsNullOrEmpty(savePath) || !System.IO.File.Exists(savePath))
            {
                OniMultiplayerMod.LogError("[Host] No MP save selected! Please select or import a save.");
                _statusMessage = "Please select a save from the MP saves list first!";
                _state = ScreenState.GameModeSelect;
                _isStartingGame = false; // Reset so user can try again
                return;
            }
            
            // Verify it's from the MP folder
            string mpFolder = Systems.MultiplayerSaveManager.Instance?.GetMPSaveFolder() ?? "";
            if (!string.IsNullOrEmpty(mpFolder) && !savePath.Contains("save_files_mp"))
            {
                OniMultiplayerMod.LogWarning($"[Host] Save is not from MP folder! Path: {savePath}");
                // Still allow it for now, but log warning
            }
            
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
            {
                OniMultiplayerMod.Log($"Loading save: {savePath}");
                
                // Store current save path for dupe assignment persistence
                Patches.SaveLoadPatches.CurrentSavePath = savePath;
                
                // Get save info to send to clients
                string fileName = System.IO.Path.GetFileName(savePath);
                string worldName = SaveGame.Instance?.BaseName ?? "Colony";
                int cycle = GameClock.Instance?.GetCycle() ?? 0;
                int dupeCount = global::Components.LiveMinionIdentities.Count;
                
                // Generate hash for validation
                string saveHash = Systems.MultiplayerSaveManager.Instance?.GenerateShortHash(savePath) ?? "";
                
                // Send GameStartPacket to all clients BEFORE loading
                var gameStartPacket = new GameStartPacket
                {
                    SaveFileName = fileName,
                    WorldName = worldName,
                    GameCycle = cycle,
                    DupeCount = dupeCount,
                    SaveHash = saveHash
                };
                
                OniMultiplayerMod.Log($"Sending GameStartPacket to clients: {fileName} (hash: {saveHash})");
                SteamP2PManager.Instance?.BroadcastToClients(gameStartPacket);
                
                // Close our UI before loading
                Deactivate();
                
                // Load the save
                LoadingOverlay.Load(() => {
                    SaveLoader.SetActiveSaveFilePath(savePath);
                    LoadScreen.DoLoad(savePath);
                    
                    // After load completes, send GameReadyPacket
                    // This is handled in GamePatches when Game.OnSpawn fires
                });
            }
            else
            {
                OniMultiplayerMod.Log("No save found!");
                _statusMessage = "No save file found! Select or import a save.";
                _isStartingGame = false; // Reset so user can try again
            }
        }

        #endregion

        /// <summary>
        /// Show the multiplayer screen.
        /// </summary>
        public static void Show()
        {
            if (Instance == null)
            {
                var go = new GameObject("MultiplayerScreen");
                Instance = go.AddComponent<MultiplayerScreen>();
                DontDestroyOnLoad(go);
            }

            Instance.Activate();
        }
    }
}