using LiteNetLib;
using LiteNetLib.Utils;
using OniMultiplayer.Patches;
using OniMultiplayer.Network;
using UnityEngine;

namespace OniMultiplayer
{
    /// <summary>
    /// Handles incoming packets on both host and client side.
    /// HOST: Receives client requests and executes game actions.
    /// CLIENT: Receives state updates and applies them visually.
    /// </summary>
    public static class PacketHandler
    {
        /// <summary>
        /// Handle packet with NetPeer (LiteNetLib).
        /// </summary>
        public static void HandlePacket(NetPeer peer, INetSerializable packet, bool isHost)
        {
            HandlePacket(peer.Id, packet, isHost);
        }

        /// <summary>
        /// Handle packet with player ID directly (Steam P2P).
        /// </summary>
        public static void HandlePacket(int playerId, INetSerializable packet, bool isHost)
        {
            if (isHost)
            {
                HandlePacketOnHost(playerId, packet);
            }
            else
            {
                HandlePacketOnClient(packet);
            }
        }

        /// <summary>
        /// Host receives packets from clients (intents/requests).
        /// </summary>
        private static void HandlePacketOnHost(int playerId, INetSerializable packet)
        {

            switch (packet)
            {
                // === Player Management ===
                case PlayerJoinPacket joinPacket:
                    HandlePlayerJoin(playerId, joinPacket);
                    break;

                // === Tool Requests ===
                case RequestDigPacket digPacket:
                    HandleDigRequest(playerId, digPacket);
                    break;

                case RequestBuildPacket buildPacket:
                    HandleBuildRequest(playerId, buildPacket);
                    break;

                case RequestDeconstructPacket deconstructPacket:
                    HandleDeconstructRequest(playerId, deconstructPacket);
                    break;

                case RequestMopPacket mopPacket:
                    HandleMopRequest(playerId, mopPacket);
                    break;

                case RequestSweepPacket sweepPacket:
                    HandleSweepRequest(playerId, sweepPacket);
                    break;

                case RequestHarvestPacket harvestPacket:
                    HandleHarvestRequest(playerId, harvestPacket);
                    break;

                case RequestAttackPacket attackPacket:
                    HandleAttackRequest(playerId, attackPacket);
                    break;

                case RequestCapturePacket capturePacket:
                    HandleCaptureRequest(playerId, capturePacket);
                    break;

                case RequestEmptyPipePacket emptyPipePacket:
                    HandleEmptyPipeRequest(playerId, emptyPipePacket);
                    break;

                case RequestDisconnectPacket disconnectPacket:
                    HandleDisconnectRequest(playerId, disconnectPacket);
                    break;

                case RequestCancelAtCellPacket cancelCellPacket:
                    HandleCancelAtCellRequest(playerId, cancelCellPacket);
                    break;

                // === Building/Chore Requests ===
                case RequestUseBuildingPacket usePacket:
                    HandleUseBuildingRequest(playerId, usePacket);
                    break;

                case RequestMoveToPacket movePacket:
                    HandleMoveToRequest(playerId, movePacket);
                    break;

                case RequestPriorityChangePacket priorityPacket:
                    HandlePriorityChangeRequest(playerId, priorityPacket);
                    break;

                case RequestCancelChorePacket cancelPacket:
                    HandleCancelChoreRequest(playerId, cancelPacket);
                    break;

                // === Speed Control ===
                case RequestSpeedChangePacket speedPacket:
                    HandleSpeedChangeRequest(playerId, speedPacket);
                    break;

                case RequestPauseTogglePacket pauseTogglePacket:
                    HandlePauseToggleRequest(playerId);
                    break;

                case RequestPausePacket pausePacket:
                    HandlePauseRequest(playerId, pausePacket);
                    break;

                // === Dupe Selection (in-game) ===
                case RequestDupeSelectionPacket dupeSelectPacket:
                    HandleDupeSelectionRequest(playerId, dupeSelectPacket);
                    break;

                // === Character Selection Screen (new game) ===
                case DupePickedPacket pickedPacket:
                    HandleDupePickedOnHost(playerId, pickedPacket);
                    break;

                case DupeUnpickedPacket unpickedPacket:
                    HandleDupeUnpickedOnHost(playerId, unpickedPacket);
                    break;

                // === Game Loading ===
                case PlayerLoadedPacket loadedPacket:
                    HandlePlayerLoadedOnHost(playerId, loadedPacket);
                    break;

                default:
                    OniMultiplayerMod.LogWarning($"Unhandled packet type on host: {packet.GetType().Name}");
                    break;
            }
        }

        /// <summary>
        /// Client receives packets from host (state updates).
        /// </summary>
        private static void HandlePacketOnClient(INetSerializable packet)
        {
            switch (packet)
            {
                // === Connection ===
                case WelcomePacket welcomePacket:
                    HandleWelcome(welcomePacket);
                    break;

                case DupeAssignmentPacket assignmentPacket:
                    HandleDupeAssignment(assignmentPacket);
                    break;

                // === Game Flow ===
                case GameStartPacket gameStartPacket:
                    HandleGameStart(gameStartPacket);
                    break;

                case GameReadyPacket gameReadyPacket:
                    HandleGameReady(gameReadyPacket);
                    break;

                // === Dupe State ===
                case DupeStatePacket statePacket:
                    HandleDupeState(statePacket);
                    break;

                case DupeBatchStatePacket batchPacket:
                    HandleDupeBatchState(batchPacket);
                    break;

                // === World State ===
                case WorldChunkPacket chunkPacket:
                    HandleWorldChunk(chunkPacket);
                    break;

                case TileUpdatePacket tilePacket:
                    HandleTileUpdate(tilePacket);
                    break;

                case BuildingStatePacket buildingPacket:
                    HandleBuildingState(buildingPacket);
                    break;

                case WorldChecksumPacket checksumPacket:
                    HandleWorldChecksum(checksumPacket);
                    break;

                case ChoreCompletedPacket chorePacket:
                    HandleChoreCompleted(chorePacket);
                    break;

                // === Simulation Sync ===
                case CellDugPacket cellDugPacket:
                    HandleCellDug(cellDugPacket);
                    break;

                case BuildingPlacedPacket buildingPlacedPacket:
                    HandleBuildingPlaced(buildingPlacedPacket);
                    break;

                case BuildingDestroyedPacket buildingDestroyedPacket:
                    HandleBuildingDestroyed(buildingDestroyedPacket);
                    break;

                // === Speed Control ===
                case SpeedChangePacket speedPacket:
                    HandleSpeedChange(speedPacket);
                    break;

                case PauseStatePacket pausePacket:
                    HandlePauseState(pausePacket);
                    break;

                // === Character Selection Screen (new game) ===
                case DupePickedPacket pickedPacket:
                    HandleDupePickedOnClient(pickedPacket);
                    break;

                case DupeUnpickedPacket unpickedPacket:
                    HandleDupeUnpickedOnClient(unpickedPacket);
                    break;

                case DupeSelectionProceedPacket proceedPacket:
                    HandleDupeSelectionProceed();
                    break;

                // === Game Loading ===
                case AllPlayersLoadedPacket allLoadedPacket:
                    HandleAllPlayersLoaded(allLoadedPacket);
                    break;

                case NewGameStartPacket newGamePacket:
                    HandleNewGameStart();
                    break;

                case DupeSelectionCompletePacket dupeSelectCompletePacket:
                    HandleDupeSelectionComplete();
                    break;

                case BulkDupeAssignmentPacket bulkAssignPacket:
                    HandleBulkDupeAssignment(bulkAssignPacket);
                    break;

                default:
                    OniMultiplayerMod.LogWarning($"Unhandled packet type on client: {packet.GetType().Name}");
                    break;
            }
        }

        #region Host Handlers - Player Management

        private static void HandlePlayerJoin(int playerId, PlayerJoinPacket packet)
        {
            OniMultiplayerMod.Log($"Player {playerId} joined as '{packet.PlayerName}'");
        }

        #endregion

        #region Host Handlers - Tool Requests (Execute Game Actions)

        private static void HandleDigRequest(int playerId, RequestDigPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} dig at cell {packet.Cell}");
            
            // Execute the dig action on the host
            if (Grid.IsValidCell(packet.Cell) && Grid.Solid[packet.Cell])
            {
                // Register cell ownership BEFORE creating the dig errand
                ChorePatches.RegisterCellOwnership(packet.Cell, playerId);
                
                // Create dig errand using DigTool's static method
                DigTool.PlaceDig(packet.Cell, 0);
                
                // Set priority if available
                var go = Grid.Objects[packet.Cell, (int)ObjectLayer.DigPlacer];
                if (go != null)
                {
                    var prioritizable = go.GetComponent<Prioritizable>();
                    if (prioritizable != null)
                    {
                        prioritizable.SetMasterPriority(new PrioritySetting(
                            PriorityScreen.PriorityClass.basic, 
                            packet.Priority));
                    }
                }
            }
        }

        private static void HandleBuildRequest(int playerId, RequestBuildPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} build '{packet.BuildingPrefabId}' at cell {packet.Cell}");
            
            // Find the building definition
            var def = Assets.GetBuildingDef(packet.BuildingPrefabId);
            if (def == null)
            {
                OniMultiplayerMod.LogWarning($"[Host] Unknown building: {packet.BuildingPrefabId}");
                return;
            }

            // Register cell ownership BEFORE creating the build order
            ChorePatches.RegisterCellOwnership(packet.Cell, playerId);

            // Get position from cell
            Vector3 pos = Grid.CellToPosCBC(packet.Cell, def.SceneLayer);
            
            // Create build order
            var orientation = (Orientation)packet.Rotation;
            def.TryPlace(null, pos, orientation, null, null, packet.Priority);
        }

        private static void HandleDeconstructRequest(int playerId, RequestDeconstructPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} deconstruct at cell {packet.Cell}");
            
            // Register cell ownership
            ChorePatches.RegisterCellOwnership(packet.Cell, playerId);
            
            // Find building at cell
            var building = Grid.Objects[packet.Cell, (int)ObjectLayer.Building];
            if (building != null)
            {
                var deconstructable = building.GetComponent<Deconstructable>();
                if (deconstructable != null)
                {
                    deconstructable.QueueDeconstruction(true);
                }
            }
        }

        private static void HandleMopRequest(int playerId, RequestMopPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} mop at cell {packet.Cell}");
            
            if (Grid.IsValidCell(packet.Cell))
            {
                // Register cell ownership
                ChorePatches.RegisterCellOwnership(packet.Cell, playerId);
                
                // Create mop marker
                var go = Util.KInstantiate(Assets.GetPrefab("MopPlacer"));
                if (go != null)
                {
                    go.transform.SetPosition(Grid.CellToPosCCC(packet.Cell, Grid.SceneLayer.Move));
                    go.SetActive(true);
                }
            }
        }

        private static void HandleSweepRequest(int playerId, RequestSweepPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} sweep at cell {packet.Cell}");
            
            // Register cell ownership
            ChorePatches.RegisterCellOwnership(packet.Cell, playerId);
            
            // Find all Pickupables at cell and mark for sweep
            // Signature: MarkForClear(Boolean restoringFromSave, Boolean allowWhenStored)
            var pickupablesAtCell = global::Components.Pickupables.Items;
            foreach (var pickupable in pickupablesAtCell)
            {
                if (pickupable != null && Grid.PosToCell(pickupable.transform.position) == packet.Cell)
                {
                    var clearable = pickupable.GetComponent<Clearable>();
                    if (clearable != null)
                    {
                        clearable.MarkForClear(false, true); // Not restoring from save, allow when stored
                    }
                }
            }
        }

        private static void HandleHarvestRequest(int playerId, RequestHarvestPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} harvest at cell {packet.Cell}");
            
            // Register cell ownership
            ChorePatches.RegisterCellOwnership(packet.Cell, playerId);
            
            // Find harvestable at cell
            var go = Grid.Objects[packet.Cell, (int)ObjectLayer.Building];
            if (go != null)
            {
                var harvestable = go.GetComponent<Harvestable>();
                if (harvestable != null && harvestable.CanBeHarvested)
                {
                    // Force harvest via the harvestDesignatable system
                    var harvestDesignatable = go.GetComponent<HarvestDesignatable>();
                    if (harvestDesignatable != null)
                    {
                        harvestDesignatable.SetHarvestWhenReady(true);
                    }
                    OniMultiplayerMod.Log($"[Host] Harvest requested at cell {packet.Cell}");
                }
            }
        }

        private static void HandleAttackRequest(int playerId, RequestAttackPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} attack at cell {packet.Cell}");
            
            // Register cell ownership
            ChorePatches.RegisterCellOwnership(packet.Cell, playerId);
            
            // Find attackable creature at cell
            var creature = Grid.Objects[packet.Cell, (int)ObjectLayer.Pickupables];
            if (creature != null)
            {
                var factionAlignment = creature.GetComponent<FactionAlignment>();
                if (factionAlignment != null)
                {
                    factionAlignment.SetPlayerTargeted(true);
                }
            }
        }

        private static void HandleCaptureRequest(int playerId, RequestCapturePacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} capture at cell {packet.Cell}");
            
            // Register cell ownership
            ChorePatches.RegisterCellOwnership(packet.Cell, playerId);
            
            // Find capturable creature at cell
            var creature = Grid.Objects[packet.Cell, (int)ObjectLayer.Pickupables];
            if (creature != null)
            {
                var capturable = creature.GetComponent<Capturable>();
                if (capturable != null)
                {
                    capturable.MarkForCapture(true);
                }
            }
        }

        private static void HandleEmptyPipeRequest(int playerId, RequestEmptyPipePacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} empty pipe at cell {packet.Cell}");
            
            // Find conduit at cell
            var conduit = Grid.Objects[packet.Cell, (int)ObjectLayer.LiquidConduit];
            if (conduit != null)
            {
                var emptyCond = conduit.GetComponent<EmptyConduitWorkable>();
                if (emptyCond != null)
                {
                    // Queue empty
                }
            }
        }

        private static void HandleDisconnectRequest(int playerId, RequestDisconnectPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} disconnect at cell {packet.Cell}");
            
            // Find utility network at cell and disconnect
            // This varies by utility type (electric, pipe, etc.)
        }

        private static void HandleCancelAtCellRequest(int playerId, RequestCancelAtCellPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} cancel at cell {packet.Cell}");
            
            // Cancel any pending errands at this cell
            // Check for dig placers, build placers, etc.
            for (int layer = 0; layer < (int)ObjectLayer.NumLayers; layer++)
            {
                var obj = Grid.Objects[packet.Cell, layer];
                if (obj != null)
                {
                    // Try to cancel via Prioritizable's cancel
                    var prioritizable = obj.GetComponent<Prioritizable>();
                    if (prioritizable != null)
                    {
                        // Destroy the placer object to cancel the errand
                        UnityEngine.Object.Destroy(obj);
                    }
                }
            }
        }

        #endregion

        #region Host Handlers - Building/Chore Requests

        private static void HandleUseBuildingRequest(int playerId, RequestUseBuildingPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} use building {packet.BuildingInstanceId} ({packet.InteractionType})");
            ChoreManager.Instance?.QueueUseBuildingChore(playerId, packet.BuildingInstanceId, packet.InteractionType);
        }

        private static void HandleMoveToRequest(int playerId, RequestMoveToPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} move to cell {packet.TargetCell}");
            ChoreManager.Instance?.QueueMoveToChore(playerId, packet.TargetCell);
        }

        private static void HandlePriorityChangeRequest(int playerId, RequestPriorityChangePacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} priority change for {packet.TargetInstanceId} to {packet.NewPriority}");
            
            // Find object by instance ID and change priority
            foreach (var prioritizable in UnityEngine.Object.FindObjectsOfType<Prioritizable>())
            {
                if (prioritizable.gameObject.GetInstanceID() == packet.TargetInstanceId)
                {
                    prioritizable.SetMasterPriority(new PrioritySetting(
                        PriorityScreen.PriorityClass.basic,
                        packet.NewPriority));
                    break;
                }
            }
        }

        private static void HandleCancelChoreRequest(int playerId, RequestCancelChorePacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} cancel chore {packet.ChoreId}");
            ChoreManager.Instance?.CancelChore(playerId, packet.ChoreId);
        }

        #endregion

        #region Host Handlers - Speed Control

        private static void HandleSpeedChangeRequest(int playerId, RequestSpeedChangePacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} requests speed {packet.RequestedSpeed}");
            
            // Host decides whether to allow - for now, allow all
            if (SpeedControlScreen.Instance != null)
            {
                SpeedControlScreen.Instance.SetSpeed(packet.RequestedSpeed);
            }
        }

        private static void HandlePauseToggleRequest(int playerId)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} requests pause toggle");
            
            if (SpeedControlScreen.Instance != null)
            {
                SpeedControlScreen.Instance.TogglePause();
            }
        }

        private static void HandlePauseRequest(int playerId, RequestPausePacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} requests pause={packet.Pause}");
            
            if (SpeedControlScreen.Instance != null)
            {
                if (packet.Pause)
                    SpeedControlScreen.Instance.Pause();
                else
                    SpeedControlScreen.Instance.Unpause();
            }
        }

        private static void HandleDupeSelectionRequest(int playerId, RequestDupeSelectionPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} requests dupe '{packet.DupeName}'");
            
            // Check if dupe is already assigned to someone else
            int currentOwner = DupeOwnership.Instance?.GetOwnerPlayerByName(packet.DupeName) ?? -1;
            
            if (currentOwner >= 0 && currentOwner != playerId)
            {
                OniMultiplayerMod.Log($"[Host] Dupe '{packet.DupeName}' already owned by player {currentOwner}");
                return;
            }
            
            // Register ownership locally (using name-based method)
            DupeOwnership.Instance?.RegisterOwnershipByName(playerId, packet.DupeName);
            
            // Broadcast to all clients
            var assignPacket = new DupeAssignmentPacket
            {
                PlayerId = playerId,
                DupeName = packet.DupeName
            };
            
            SteamP2PManager.Instance?.BroadcastToClients(assignPacket);
            
            OniMultiplayerMod.Log($"[Host] Assigned dupe '{packet.DupeName}' to player {playerId}");
        }

        #endregion

        #region Host Handlers - Character Selection

        private static void HandleDupePickedOnHost(int playerId, DupePickedPacket packet)
        {
            // No longer used - host controls all character selection
            OniMultiplayerMod.Log($"[Host] Ignoring legacy DupePickedPacket from player {playerId}");
        }

        private static void HandleDupeUnpickedOnHost(int playerId, DupeUnpickedPacket packet)
        {
            // No longer used - host controls all character selection
            OniMultiplayerMod.Log($"[Host] Ignoring legacy DupeUnpickedPacket from player {playerId}");
        }

        private static void HandlePlayerLoadedOnHost(int playerId, PlayerLoadedPacket packet)
        {
            OniMultiplayerMod.Log($"[Host] Player {playerId} has loaded the game");
            
            // Forward to game patches
            Patches.GamePatches.OnPlayerLoaded(playerId);
        }

        #endregion

        #region Client Handlers - Character Selection

        private static void HandleDupePickedOnClient(DupePickedPacket packet)
        {
            // No longer used - host controls all character selection
            OniMultiplayerMod.Log($"[Client] Ignoring legacy DupePickedPacket");
        }

        private static void HandleDupeUnpickedOnClient(DupeUnpickedPacket packet)
        {
            // No longer used - host controls all character selection
            OniMultiplayerMod.Log($"[Client] Ignoring legacy DupeUnpickedPacket");
        }

        private static void HandleDupeSelectionProceed()
        {
            OniMultiplayerMod.Log("[Client] Host says proceed with dupe selection");
            
            // Forward to character selection patches (sets IsNewGameFromCharacterSelection)
            Patches.CharacterSelectionPatches.OnProceedSignal();
        }

        private static void HandleAllPlayersLoaded(AllPlayersLoadedPacket packet)
        {
            OniMultiplayerMod.Log($"[Client] All {packet.PlayerCount} players have loaded");
            
            // Forward to game patches - this now just means loading is done,
            // not that game should start (dupe selection still needed)
            Patches.GamePatches.OnAllPlayersLoaded();
        }

        private static void HandleNewGameStart()
        {
            OniMultiplayerMod.Log("[Client] *** RECEIVED NewGameStartPacket! ***");
            OniMultiplayerMod.Log("[Client] Host is starting a new game - following to world selection");
            
            // First, close the MultiplayerScreen if it's open
            bool screenWasActive = UI.MultiplayerScreen.Instance != null && UI.MultiplayerScreen.Instance.isActiveAndEnabled;
            OniMultiplayerMod.Log($"[Client] MultiplayerScreen active: {screenWasActive}");
            
            if (screenWasActive)
            {
                OniMultiplayerMod.Log("[Client] Deactivating MultiplayerScreen");
                UI.MultiplayerScreen.Instance.Deactivate();
            }
            
            // Re-enable MainMenu if it was hidden
            bool mainMenuExists = MainMenu.Instance != null;
            OniMultiplayerMod.Log($"[Client] MainMenu.Instance exists: {mainMenuExists}");
            
            if (mainMenuExists)
            {
                OniMultiplayerMod.Log("[Client] Setting MainMenu active");
                MainMenu.Instance.gameObject.SetActive(true);
                
                var newGameMethod = typeof(MainMenu).GetMethod("NewGame", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    
                if (newGameMethod != null)
                {
                    OniMultiplayerMod.Log("[Client] Invoking MainMenu.NewGame() via reflection...");
                    newGameMethod.Invoke(MainMenu.Instance, null);
                    OniMultiplayerMod.Log("[Client] NewGame() invoked successfully!");
                }
                else
                {
                    OniMultiplayerMod.LogError("[Client] Could not find MainMenu.NewGame method!");
                }
            }
            else
            {
                OniMultiplayerMod.LogError("[Client] MainMenu.Instance is null - may already be in game");
            }
        }

        private static void HandleDupeSelectionComplete()
        {
            OniMultiplayerMod.Log("[Client] Dupe selection complete - starting game");
            
            // Snap camera to our first dupe
            Components.DupeCameraFollow.Instance?.SnapToMyDupe();
        }

        #endregion

        #region Client Handlers - Connection

        private static void HandleWelcome(WelcomePacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Welcome! Player ID: {packet.AssignedPlayerId}, Host: {packet.HostPlayerName}");
            // LocalPlayerId is already set by Steam P2P welcome handling
        }

        private static void HandleDupeAssignment(DupeAssignmentPacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Dupe '{packet.DupeName}' assigned to player {packet.PlayerId}");
            
            if (packet.PlayerId >= 0)
            {
                DupeOwnership.Instance?.RegisterOwnershipByName(packet.PlayerId, packet.DupeName);
            }
            else
            {
                // PlayerId = -1 means unassign/release
                DupeOwnership.Instance?.UnregisterOwnershipByName(packet.DupeName);
            }
            
            // If this is our dupe, show notification
            int localPlayerId = SteamP2PManager.Instance?.LocalPlayerId ?? -1;
            if (packet.PlayerId == localPlayerId)
            {
                OniMultiplayerMod.Log($"[Client] YOU control this dupe!");
            }
        }

        private static void HandleBulkDupeAssignment(BulkDupeAssignmentPacket packet)
        {
            if (packet.Assignments == null || packet.Assignments.Length == 0)
            {
                OniMultiplayerMod.Log("[Client] Received empty bulk dupe assignment");
                return;
            }

            OniMultiplayerMod.Log($"[Client] Received bulk dupe assignment ({packet.Assignments.Length} assignments)");

            int localPlayerId = SteamP2PManager.Instance?.LocalPlayerId ?? -1;
            int myDupeCount = 0;

            foreach (var assignment in packet.Assignments)
            {
                DupeOwnership.Instance?.RegisterOwnershipByName(assignment.PlayerId, assignment.DupeName);
                
                if (assignment.PlayerId == localPlayerId)
                {
                    myDupeCount++;
                }
            }

            OniMultiplayerMod.Log($"[Client] You control {myDupeCount} dupes");
            
            // Snap camera to our first dupe
            Components.DupeCameraFollow.Instance?.SnapToMyDupe();
        }

        #endregion

        #region Client Handlers - Game Flow

        private static void HandleGameStart(GameStartPacket packet)
        {
            OniMultiplayerMod.Log($"[Client] *** RECEIVED GameStartPacket! ***");
            OniMultiplayerMod.Log($"[Client] Save: {packet.SaveFileName}, World: {packet.WorldName}, Cycle: {packet.GameCycle}, Hash: {packet.SaveHash}");
            
            // First, try to find in MP saves folder
            string savePath = Systems.MultiplayerSaveManager.Instance?.FindMPSave(packet.SaveFileName);
            
            // Fallback to general search if not in MP folder
            if (string.IsNullOrEmpty(savePath))
            {
                savePath = FindSaveFile(packet.SaveFileName);
            }
            
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
            {
                OniMultiplayerMod.Log($"[Client] Found save file: {savePath}");
                
                // Validate hash if provided
                if (!string.IsNullOrEmpty(packet.SaveHash))
                {
                    bool hashValid = Systems.MultiplayerSaveManager.Instance?.ValidateSaveHash(packet.SaveFileName, packet.SaveHash) ?? true;
                    
                    if (!hashValid)
                    {
                        string clientHash = Systems.MultiplayerSaveManager.Instance?.GenerateShortHash(savePath) ?? "unknown";
                        OniMultiplayerMod.LogError($"[Client] SAVE MISMATCH! Your save file is different from the host's.");
                        OniMultiplayerMod.LogError($"[Client] Expected hash: {packet.SaveHash}, Your hash: {clientHash}");
                        OniMultiplayerMod.LogError("[Client] Please get the latest save file from the host!");
                        
                        // Show error to user
                        ShowSaveMismatchError(packet.SaveFileName, packet.SaveHash, clientHash);
                        return;
                    }
                    
                    OniMultiplayerMod.Log("[Client] Save hash validated successfully!");
                }
                
                // Set flag to allow client load through SaveLoadPatches
                SaveLoadPatches.ClientLoadingFromHost = true;
                
                // Load the save file
                LoadingOverlay.Load(() => {
                    SaveLoader.SetActiveSaveFilePath(savePath);
                    LoadScreen.DoLoad(savePath);
                });
            }
            else
            {
                OniMultiplayerMod.LogError($"[Client] Save file not found: {packet.SaveFileName}");
                OniMultiplayerMod.LogError("[Client] Make sure you have the same save file as the host!");
                
                // Show error to user
                ShowSaveNotFoundError(packet.SaveFileName);
            }
        }

        private static void ShowSaveMismatchError(string fileName, string expectedHash, string actualHash)
        {
            string mpFolder = Systems.MultiplayerSaveManager.Instance?.GetMPSaveFolder() ?? "";
            
            // Show notification to user
            UI.MultiplayerNotification.ShowError(
                $"Save file mismatch! Your '{fileName}' is different from host's.\n" +
                $"Expected: {expectedHash}, Yours: {actualHash}\n" +
                $"Get the latest save from host.", 
                15f
            );
            
            // Log full details
            OniMultiplayerMod.LogError($"Save mismatch - File: {fileName}, Expected: {expectedHash}, Got: {actualHash}");
            OniMultiplayerMod.LogError($"MP saves folder: {mpFolder}");
        }

        private static void ShowSaveNotFoundError(string fileName)
        {
            string mpFolder = Systems.MultiplayerSaveManager.Instance?.GetMPSaveFolder() ?? "";
            
            // Show notification to user
            UI.MultiplayerNotification.ShowError(
                $"Save file '{fileName}' not found!\n" +
                $"Get the save from host and place in:\n{mpFolder}",
                15f
            );
            
            OniMultiplayerMod.LogError($"Save not found: {fileName}");
            OniMultiplayerMod.LogError($"MP saves folder: {mpFolder}");
        }

        private static void HandleGameReady(GameReadyPacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Host game ready. Time: {packet.GameTime}, Paused: {packet.IsPaused}, Speed: {packet.Speed}");
            
            // Sync game state with host
            if (SpeedControlScreen.Instance != null)
            {
                // Set initial pause state
                if (packet.IsPaused)
                {
                    Time.timeScale = 0f;
                }
                else
                {
                    Time.timeScale = packet.Speed;
                }
            }
        }

        /// <summary>
        /// Find a save file by name in the user's save folders.
        /// </summary>
        private static string FindSaveFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            
            // First, try to find based on the latest save's directory
            string latestSave = SaveLoader.GetLatestSaveForCurrentDLC();
            if (!string.IsNullOrEmpty(latestSave))
            {
                string latestDir = System.IO.Path.GetDirectoryName(latestSave);
                if (!string.IsNullOrEmpty(latestDir))
                {
                    string targetPath = System.IO.Path.Combine(latestDir, fileName);
                    if (System.IO.File.Exists(targetPath))
                    {
                        return targetPath;
                    }
                    
                    // Also check parent directory (save_files folder)
                    string parentDir = System.IO.Path.GetDirectoryName(latestDir);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        // Search all colony folders
                        try
                        {
                            var files = System.IO.Directory.GetFiles(parentDir, fileName, System.IO.SearchOption.AllDirectories);
                            if (files.Length > 0)
                            {
                                return files[0];
                            }
                        }
                        catch { }
                    }
                }
            }
            
            // Try common save location
            string savePrefix = SaveLoader.GetSavePrefixAndCreateFolder();
            if (!string.IsNullOrEmpty(savePrefix))
            {
                // Direct path
                string directPath = System.IO.Path.Combine(savePrefix, fileName);
                if (System.IO.File.Exists(directPath))
                {
                    return directPath;
                }
                
                // Search subdirectories
                if (System.IO.Directory.Exists(savePrefix))
                {
                    try
                    {
                        var files = System.IO.Directory.GetFiles(savePrefix, fileName, System.IO.SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            return files[0];
                        }
                    }
                    catch { }
                }
            }
            
            // Try Documents folder path
            string docsPath = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), 
                "Klei", "OxygenNotIncluded", "save_files");
                
            if (System.IO.Directory.Exists(docsPath))
            {
                try
                {
                    var files = System.IO.Directory.GetFiles(docsPath, fileName, System.IO.SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
                catch { }
            }

            return null;
        }

        #endregion

        #region Client Handlers - Dupe State

        private static void HandleDupeState(DupeStatePacket packet)
        {
            DupeSyncManager.Instance?.ApplyDupeState(packet);
        }

        private static void HandleDupeBatchState(DupeBatchStatePacket packet)
        {
            if (packet.Dupes != null)
            {
                foreach (var dupeState in packet.Dupes)
                {
                    DupeSyncManager.Instance?.ApplyDupeState(dupeState);
                }
            }
        }

        #endregion

        #region Client Handlers - World State

        private static void HandleWorldChunk(WorldChunkPacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Received world chunk at ({packet.ChunkX}, {packet.ChunkY})");
            WorldSyncManager.Instance?.ApplyWorldChunk(packet);
        }

        private static void HandleTileUpdate(TileUpdatePacket packet)
        {
            WorldSyncManager.Instance?.ApplyTileUpdate(packet);
        }

        private static void HandleBuildingState(BuildingStatePacket packet)
        {
            WorldSyncManager.Instance?.ApplyBuildingState(packet);
        }

        private static void HandleWorldChecksum(WorldChecksumPacket packet)
        {
            WorldSyncManager.Instance?.VerifyChecksum(packet);
        }

        private static void HandleChoreCompleted(ChoreCompletedPacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Chore {packet.ChoreId} completed by dupe '{packet.DupeName}'");
        }

        #endregion

        #region Client Handlers - Simulation Sync

        private static void HandleCellDug(CellDugPacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Cell {packet.Cell} was dug - syncing to client");
            
            if (!Grid.IsValidCell(packet.Cell))
            {
                OniMultiplayerMod.LogWarning($"[Client] Invalid cell {packet.Cell} in CellDugPacket");
                return;
            }

            try
            {
                // Use SimMessages.ModifyCell to update the cell state
                // Setting mass to 0 effectively makes the cell "empty"
                // The element type doesn't matter much when mass is 0
                SimMessages.ModifyCell(
                    packet.Cell,
                    packet.ElementIdx,      // Keep element type (doesn't matter with 0 mass)
                    packet.Temperature,
                    0f,                     // ZERO MASS = cell is empty/dug out
                    packet.DiseaseIdx,
                    packet.DiseaseCount,
                    SimMessages.ReplaceType.Replace,
                    false,                  // No vertical displacement
                    -1                      // No callback
                );

                OniMultiplayerMod.Log($"[Client] Applied SimMessages.ModifyCell for cell {packet.Cell}");

                // Trigger visual refresh
                World.Instance?.OnSolidChanged(packet.Cell);
            }
            catch (System.Exception ex)
            {
                OniMultiplayerMod.LogError($"[Client] Failed to apply CellDug via SimMessages: {ex.Message}");
                // Note: Direct Grid modification is NOT possible - arrays are read-only
                // SimMessages is the ONLY way to modify cell state
            }
        }

        private static void HandleBuildingPlaced(BuildingPlacedPacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Building {packet.PrefabId} placed at cell {packet.Cell}");
            
            if (!Grid.IsValidCell(packet.Cell))
            {
                OniMultiplayerMod.LogWarning($"[Client] Invalid cell {packet.Cell} for building");
                return;
            }

            try
            {
                // Check if building already exists at this cell (host already has it)
                var existingBuilding = Grid.Objects[packet.Cell, (int)ObjectLayer.Building];
                if (existingBuilding != null)
                {
                    OniMultiplayerMod.Log($"[Client] Building already exists at cell {packet.Cell}, skipping spawn");
                    return;
                }

                // Try to spawn the building on client
                var buildingDef = Assets.GetBuildingDef(packet.PrefabId);
                if (buildingDef != null)
                {
                    var orientation = (Orientation)packet.Rotation;
                    float temperature = packet.Temperature > 0 ? packet.Temperature : 293f;
                    
                    // Spawn the completed building
                    var building = buildingDef.Build(
                        packet.Cell,
                        orientation,
                        null,  // No storage
                        buildingDef.DefaultElements(),
                        temperature,
                        false, // Not from placeBuilding
                        GameClock.Instance?.GetTime() ?? 0f
                    );
                    
                    if (building != null)
                    {
                        OniMultiplayerMod.Log($"[Client] Spawned building {packet.PrefabId} at {packet.Cell}");
                    }
                }
                else
                {
                    OniMultiplayerMod.LogWarning($"[Client] Building def not found: {packet.PrefabId}");
                }
            }
            catch (System.Exception ex)
            {
                OniMultiplayerMod.LogError($"[Client] Failed to spawn building: {ex.Message}");
            }
        }

        private static void HandleBuildingDestroyed(BuildingDestroyedPacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Building at cell {packet.Cell} destroyed (ID: {packet.InstanceId})");
            
            if (!Grid.IsValidCell(packet.Cell))
            {
                return;
            }

            try
            {
                // Find and destroy the building at this cell
                var building = Grid.Objects[packet.Cell, (int)ObjectLayer.Building];
                if (building != null)
                {
                    // Check if it's the right building by instance ID or just destroy whatever's there
                    var deconstructable = building.GetComponent<Deconstructable>();
                    if (deconstructable != null)
                    {
                        // Instant destroy without animation/resources
                        UnityEngine.Object.Destroy(building);
                        OniMultiplayerMod.Log($"[Client] Destroyed building at cell {packet.Cell}");
                    }
                    else
                    {
                        // Just destroy the GameObject
                        UnityEngine.Object.Destroy(building);
                        OniMultiplayerMod.Log($"[Client] Force destroyed object at cell {packet.Cell}");
                    }
                }
                else
                {
                    OniMultiplayerMod.LogWarning($"[Client] No building found at cell {packet.Cell} to destroy");
                }
                
                // Trigger visual refresh
                World.Instance?.OnSolidChanged(packet.Cell);
            }
            catch (System.Exception ex)
            {
                OniMultiplayerMod.LogError($"[Client] Failed to destroy building: {ex.Message}");
            }
        }

        #endregion

        #region Client Handlers - Speed Control

        private static void HandleSpeedChange(SpeedChangePacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Speed changed to {packet.Speed}");
            
            // Apply speed change locally (bypassing our patch)
            if (SpeedControlScreen.Instance != null)
            {
                // Use reflection to set speed without triggering our patch
                var field = typeof(SpeedControlScreen).GetField("speed", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(SpeedControlScreen.Instance, packet.Speed);
                }
                
                // Update time scale
                Time.timeScale = packet.Speed == 0 ? 0f : packet.Speed;
            }
        }

        private static void HandlePauseState(PauseStatePacket packet)
        {
            OniMultiplayerMod.Log($"[Client] Pause state: {packet.IsPaused}");
            
            if (SpeedControlScreen.Instance != null)
            {
                if (packet.IsPaused)
                {
                    Time.timeScale = 0f;
                }
                else
                {
                    // Restore to current speed
                    var field = typeof(SpeedControlScreen).GetField("speed",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    int speed = field != null ? (int)field.GetValue(SpeedControlScreen.Instance) : 1;
                    Time.timeScale = speed;
                }
            }
        }

        #endregion
    }
}