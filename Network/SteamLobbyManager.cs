using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace OniMultiplayer.Network
{
    /// <summary>
    /// Manages Steam Lobbies for multiplayer matchmaking.
    /// Provides: Create lobby, browse lobbies, invite friends, join via Steam overlay.
    /// </summary>
    public class SteamLobbyManager
    {
        public static SteamLobbyManager Instance { get; private set; }

        // Current lobby
        public CSteamID CurrentLobby { get; private set; } = CSteamID.Nil;
        public bool IsInLobby => CurrentLobby.IsValid() && CurrentLobby != CSteamID.Nil;
        public bool IsLobbyOwner => IsInLobby && SteamMatchmaking.GetLobbyOwner(CurrentLobby) == SteamUser.GetSteamID();
        
        // Track if game has started (to prevent re-triggering)
        private bool _gameStarted = false;
        
        // Deduplication: track last joined lobby to prevent duplicate join events
        private CSteamID _lastProcessedLobbyJoin = CSteamID.Nil;
        
        // Track the original host when we joined (for host leave detection)
        private CSteamID _originalHostId = CSteamID.Nil;

        // Lobby settings - max 3 players
        public const int MaxPlayers = 3;
        public const string LobbyDataKey_GameVersion = "game_version";
        public const string LobbyDataKey_HostName = "host_name";
        public const string LobbyDataKey_WorldName = "world_name";
        public const string GameVersion = "0.1.0";

        // Events (use delegates for proper += support)
        public delegate void LobbyListReceivedHandler(List<LobbyInfo> lobbies);
        public delegate void LobbyIdHandler(CSteamID lobbyId);
        public delegate void VoidHandler();
        public delegate void PlayerJoinedHandler(CSteamID steamId, string name);
        public delegate void PlayerLeftHandler(CSteamID steamId);
        
        public event LobbyListReceivedHandler OnLobbyListReceived;
        public event LobbyIdHandler OnLobbyCreated;
        public event LobbyIdHandler OnLobbyJoined;
        public event VoidHandler OnLobbyLeft;
        public event PlayerJoinedHandler OnPlayerJoinedLobby;
        public event PlayerLeftHandler OnPlayerLeftLobby;
        public event LobbyIdHandler OnGameStartRequested; // Host starts the game

        // Steam Callbacks
        private Callback<LobbyCreated_t> _lobbyCreatedCallback;
        private Callback<LobbyMatchList_t> _lobbyListCallback;
        private Callback<LobbyEnter_t> _lobbyEnterCallback;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;
        private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;
        private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;

        // Cached lobby list
        private List<LobbyInfo> _cachedLobbies = new List<LobbyInfo>();

        public static void Initialize()
        {
            if (Instance != null) return;

            if (!SteamManager.Initialized)
            {
                OniMultiplayerMod.LogError("Steam not initialized! Cannot use Steam lobbies.");
                return;
            }

            Instance = new SteamLobbyManager();
            Instance.RegisterCallbacks();
            OniMultiplayerMod.Log("SteamLobbyManager initialized");
        }

        private void RegisterCallbacks()
        {
            _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
            _lobbyListCallback = Callback<LobbyMatchList_t>.Create(OnLobbyListCallback);
            _lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnterCallback);
            _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdateCallback);
            _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdateCallback);
            _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequestedCallback);
        }

        /// <summary>
        /// Create a new lobby. Other players can find it or be invited.
        /// </summary>
        public void CreateLobby(string lobbyName, bool isPublic = true)
        {
            if (IsInLobby)
            {
                OniMultiplayerMod.LogWarning("Already in a lobby!");
                return;
            }

            var lobbyType = isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly;
            SteamMatchmaking.CreateLobby(lobbyType, MaxPlayers);
            OniMultiplayerMod.Log($"Creating {(isPublic ? "public" : "friends-only")} lobby: {lobbyName}");
        }

        /// <summary>
        /// Request list of available lobbies.
        /// </summary>
        public void RefreshLobbyList()
        {
            // Filter by our game version
            SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyDataKey_GameVersion, GameVersion, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.RequestLobbyList();
            OniMultiplayerMod.Log("Requesting lobby list...");
        }

        /// <summary>
        /// Join a lobby by its Steam ID.
        /// </summary>
        public void JoinLobby(CSteamID lobbyId)
        {
            if (IsInLobby)
            {
                LeaveLobby();
            }

            SteamMatchmaking.JoinLobby(lobbyId);
            OniMultiplayerMod.Log($"Joining lobby {lobbyId}...");
        }

        /// <summary>
        /// Leave the current lobby.
        /// </summary>
        public void LeaveLobby()
        {
            if (!IsInLobby) return;

            SteamMatchmaking.LeaveLobby(CurrentLobby);
            CurrentLobby = CSteamID.Nil;
            _gameStarted = false; // Reset for next lobby
            _lastProcessedLobbyJoin = CSteamID.Nil; // Reset deduplication flag
            _originalHostId = CSteamID.Nil; // Reset original host
            OnLobbyLeft?.Invoke();
            OniMultiplayerMod.Log("Left lobby");
        }

        /// <summary>
        /// Invite a Steam friend to the current lobby.
        /// </summary>
        public void InviteFriend(CSteamID friendId)
        {
            if (!IsInLobby)
            {
                OniMultiplayerMod.LogWarning("Not in a lobby!");
                return;
            }

            SteamMatchmaking.InviteUserToLobby(CurrentLobby, friendId);
            OniMultiplayerMod.Log($"Invited {SteamFriends.GetFriendPersonaName(friendId)} to lobby");
        }

        /// <summary>
        /// Open the Steam overlay to invite friends.
        /// </summary>
        public void OpenSteamInviteOverlay()
        {
            if (!IsInLobby)
            {
                OniMultiplayerMod.LogWarning("Not in a lobby!");
                return;
            }

            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
        }

        /// <summary>
        /// Get list of players in the current lobby.
        /// </summary>
        public List<LobbyPlayer> GetLobbyPlayers()
        {
            var players = new List<LobbyPlayer>();
            if (!IsInLobby) return players;

            int numMembers = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
            CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(CurrentLobby);

            for (int i = 0; i < numMembers; i++)
            {
                CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i);
                players.Add(new LobbyPlayer
                {
                    SteamId = memberId,
                    Name = SteamFriends.GetFriendPersonaName(memberId),
                    IsHost = memberId == ownerId
                });
            }

            return players;
        }

        /// <summary>
        /// Reset game state - call when returning to main menu.
        /// </summary>
        public void ResetGameState()
        {
            _gameStarted = false;
            OniMultiplayerMod.Log("SteamLobbyManager: Game state reset");
        }

        /// <summary>
        /// Host: Start the game for all lobby members.
        /// </summary>
        public void StartGame()
        {
            if (!IsLobbyOwner)
            {
                OniMultiplayerMod.LogWarning("Only lobby owner can start the game!");
                return;
            }

            if (_gameStarted)
            {
                OniMultiplayerMod.LogWarning("Game already started!");
                return;
            }

            _gameStarted = true;
            
            // Set lobby data to signal game start
            SteamMatchmaking.SetLobbyData(CurrentLobby, "game_started", "true");
            
            // This will trigger OnGameStartRequested for all members
            OnGameStartRequested?.Invoke(CurrentLobby);
        }

        #region Steam Callbacks

        private void OnLobbyCreatedCallback(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                OniMultiplayerMod.LogError($"Failed to create lobby: {result.m_eResult}");
                return;
            }

            CurrentLobby = new CSteamID(result.m_ulSteamIDLobby);
            
            // We are the host - store our own ID
            _originalHostId = SteamUser.GetSteamID();
            _lastProcessedLobbyJoin = CurrentLobby; // Mark as processed to prevent duplicate join events

            // Set lobby metadata
            SteamMatchmaking.SetLobbyData(CurrentLobby, LobbyDataKey_GameVersion, GameVersion);
            SteamMatchmaking.SetLobbyData(CurrentLobby, LobbyDataKey_HostName, SteamFriends.GetPersonaName());
            SteamMatchmaking.SetLobbyData(CurrentLobby, LobbyDataKey_WorldName, SaveGame.Instance?.BaseName ?? "New Colony");

            OniMultiplayerMod.Log($"Lobby created: {CurrentLobby}");
            OnLobbyCreated?.Invoke(CurrentLobby);
        }

        private void OnLobbyListCallback(LobbyMatchList_t result)
        {
            _cachedLobbies.Clear();

            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                
                var info = new LobbyInfo
                {
                    LobbyId = lobbyId,
                    HostName = SteamMatchmaking.GetLobbyData(lobbyId, LobbyDataKey_HostName),
                    WorldName = SteamMatchmaking.GetLobbyData(lobbyId, LobbyDataKey_WorldName),
                    PlayerCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId),
                    MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId)
                };

                _cachedLobbies.Add(info);
            }

            OniMultiplayerMod.Log($"Found {_cachedLobbies.Count} lobbies");
            OnLobbyListReceived?.Invoke(_cachedLobbies);
        }

        private void OnLobbyEnterCallback(LobbyEnter_t result)
        {
            if (result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                OniMultiplayerMod.LogError($"Failed to join lobby: {result.m_EChatRoomEnterResponse}");
                return;
            }

            CSteamID lobbyId = new CSteamID(result.m_ulSteamIDLobby);
            
            // Prevent duplicate lobby join events
            if (_lastProcessedLobbyJoin == lobbyId)
            {
                return; // Already processed this lobby join
            }
            _lastProcessedLobbyJoin = lobbyId;
            
            CurrentLobby = lobbyId;
            
            // Store the original host for leave detection
            _originalHostId = SteamMatchmaking.GetLobbyOwner(lobbyId);
            OniMultiplayerMod.Log($"Joined lobby: {CurrentLobby} (host: {_originalHostId})");
            
            OnLobbyJoined?.Invoke(CurrentLobby);
        }

        private void OnLobbyChatUpdateCallback(LobbyChatUpdate_t result)
        {
            CSteamID lobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CSteamID userId = new CSteamID(result.m_ulSteamIDUserChanged);
            EChatMemberStateChange stateChange = (EChatMemberStateChange)result.m_rgfChatMemberStateChange;

            if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeEntered))
            {
                string playerName = SteamFriends.GetFriendPersonaName(userId);
                OniMultiplayerMod.Log($"Player joined lobby: {playerName}");
                OnPlayerJoinedLobby?.Invoke(userId, playerName);
            }
            else if (stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft) ||
                     stateChange.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected))
            {
                OniMultiplayerMod.Log($"Player left lobby: {userId}");
                OnPlayerLeftLobby?.Invoke(userId);
            }
        }

        private void OnLobbyDataUpdateCallback(LobbyDataUpdate_t result)
        {
            // Check if game started
            if (result.m_bSuccess == 1)
            {
                CSteamID lobbyId = new CSteamID(result.m_ulSteamIDLobby);
                string gameStarted = SteamMatchmaking.GetLobbyData(lobbyId, "game_started");
                
                // Only trigger once - check _gameStarted flag
                if (gameStarted == "true" && !IsLobbyOwner && !_gameStarted)
                {
                    _gameStarted = true;
                    // Non-host members: game is starting
                    OnGameStartRequested?.Invoke(lobbyId);
                }
            }
        }

        private void OnGameLobbyJoinRequestedCallback(GameLobbyJoinRequested_t result)
        {
            // User clicked "Join Game" from Steam Friends list or invite
            OniMultiplayerMod.Log($"Join request from Steam overlay for lobby {result.m_steamIDLobby}");
            
            // Open the multiplayer screen first (so event handlers are registered)
            UI.MultiplayerScreen.Show();
            
            // Then join the lobby
            JoinLobby(result.m_steamIDLobby);
        }

        #endregion
    }

    /// <summary>
    /// Info about a lobby in the browse list.
    /// </summary>
    public class LobbyInfo
    {
        public CSteamID LobbyId;
        public string HostName;
        public string WorldName;
        public int PlayerCount;
        public int MaxPlayers;
    }

    /// <summary>
    /// Info about a player in a lobby.
    /// </summary>
    public class LobbyPlayer
    {
        public CSteamID SteamId;
        public string Name;
        public bool IsHost;
    }
}