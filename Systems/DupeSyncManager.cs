using System.Collections.Generic;
using OniMultiplayer.Network;
using OniMultiplayer.Systems;
using UnityEngine;

namespace OniMultiplayer
{
    /// <summary>
    /// Manages syncing dupe state between host and clients.
    /// 
    /// HOST-AUTHORITATIVE ARCHITECTURE:
    /// - Host: Collects and broadcasts authoritative dupe states
    /// - Client: Receives and applies dupe states with LAG COMPENSATION
    /// - Client does NOT run simulation - only displays host state
    /// 
    /// Uses dupe NAMES for network identification (consistent across machines).
    /// 
    /// Lag Compensation Features:
    /// - Position interpolation (smooth movement)
    /// - State buffering (handles packet loss/reordering)
    /// - Velocity prediction (reduces perceived lag)
    /// </summary>
    public class DupeSyncManager
    {
        public static DupeSyncManager Instance { get; private set; }

        // Client-side interpolation data per dupe (keyed by dupe NAME for network safety)
        private readonly Dictionary<string, DupeInterpolationState> _interpolationStates = new Dictionary<string, DupeInterpolationState>();

        // Interpolation settings
        private const float InterpolationDuration = 0.1f;  // 100ms base interpolation
        private const float MaxExtrapolation = 0.2f;       // Max 200ms prediction
        private const int StateBufferSize = 3;             // Keep 3 states for smoothing

        // Network timing
        private float _lastPacketTime = 0f;
        private float _averagePacketInterval = 0.05f;      // ~20 ticks/sec

        public static void Initialize()
        {
            Instance = new DupeSyncManager();
            OniMultiplayerMod.Log("DupeSyncManager initialized (using dupe names for network sync)");
        }

        /// <summary>
        /// [HOST] Broadcast current state of all dupes to clients.
        /// Called at tick rate from NetworkUpdater.
        /// </summary>
        public void BroadcastDupeStates()
        {
            if (!ClientMode.IsHost) return;

            var dupeStates = CollectDupeStates();
            if (dupeStates.Length == 0) return;

            var packet = new DupeBatchStatePacket { Dupes = dupeStates };
            SteamP2PManager.Instance.BroadcastToClients(packet, reliable: false);
        }

        /// <summary>
        /// [HOST] Collect current state of all dupes in the game.
        /// Uses dupe NAMES for network-safe identification.
        /// </summary>
        private DupeStatePacket[] CollectDupeStates()
        {
            var states = new List<DupeStatePacket>();

            if (global::Components.LiveMinionIdentities.Count == 0) return states.ToArray();

            float serverTime = Time.time;

            foreach (var minionIdentity in global::Components.LiveMinionIdentities.Items)
            {
                if (minionIdentity == null || minionIdentity.gameObject == null) continue;

                var go = minionIdentity.gameObject;
                string dupeName = minionIdentity.GetProperName();
                
                // Skip dupes without valid names
                if (string.IsNullOrEmpty(dupeName)) continue;

                var state = new DupeStatePacket
                {
                    DupeName = dupeName,  // Network-safe identifier
                    Timestamp = serverTime,
                    PosX = go.transform.position.x,
                    PosY = go.transform.position.y,
                    FacingRight = true,
                    AnimName = "idle_loop",
                    AnimMode = KAnim.PlayMode.Loop,
                    CurrentChoreId = -1,
                    Stress = 0f,
                    Breath = 100f,
                    Calories = 1000f,
                    Health = 100f
                };

                // Get facing direction
                var facing = go.GetComponent<Facing>();
                if (facing != null)
                {
                    state.FacingRight = facing.GetFacing();
                }

                // Get animation state
                var animController = go.GetComponent<KBatchedAnimController>();
                if (animController != null)
                {
                    try
                    {
                        var currentAnimField = typeof(KAnimControllerBase).GetField("curAnim", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var modeField = typeof(KAnimControllerBase).GetField("mode",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (currentAnimField != null)
                        {
                            var curAnim = currentAnimField.GetValue(animController);
                            if (curAnim != null)
                            {
                                var nameField = curAnim.GetType().GetField("name", 
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                if (nameField != null)
                                {
                                    var animName = nameField.GetValue(curAnim) as string;
                                    if (!string.IsNullOrEmpty(animName))
                                    {
                                        state.AnimName = animName;
                                    }
                                }
                            }
                        }

                        if (modeField != null)
                        {
                            state.AnimMode = (KAnim.PlayMode)(int)modeField.GetValue(animController);
                        }
                    }
                    catch { }
                }

                // Get chore
                var choreDriver = go.GetComponent<ChoreDriver>();
                if (choreDriver != null)
                {
                    var currentChore = choreDriver.GetCurrentChore();
                    if (currentChore != null)
                    {
                        state.CurrentChoreId = currentChore.id;
                    }
                }

                // Get vitals
                var stressMonitor = go.GetSMI<StressMonitor.Instance>();
                if (stressMonitor != null) state.Stress = stressMonitor.stress.value;

                var calorieMonitor = go.GetSMI<CalorieMonitor.Instance>();
                if (calorieMonitor != null) state.Calories = calorieMonitor.calories.value;

                var healthComponent = go.GetComponent<Health>();
                if (healthComponent != null) state.Health = healthComponent.hitPoints;

                states.Add(state);
            }

            return states.ToArray();
        }

        /// <summary>
        /// [CLIENT] Apply received dupe state from host with lag compensation.
        /// Uses dupe NAME for network-safe lookup.
        /// </summary>
        public void ApplyDupeState(DupeStatePacket packet)
        {
            // Don't apply on host - host has authoritative state
            if (ClientMode.IsHost) return;
            if (string.IsNullOrEmpty(packet.DupeName)) return;

            // Update network timing
            float currentTime = Time.time;
            if (_lastPacketTime > 0)
            {
                float interval = currentTime - _lastPacketTime;
                _averagePacketInterval = Mathf.Lerp(_averagePacketInterval, interval, 0.1f);
            }
            _lastPacketTime = currentTime;

            // Get or create interpolation state (keyed by dupe name)
            if (!_interpolationStates.TryGetValue(packet.DupeName, out var interpState))
            {
                interpState = new DupeInterpolationState();
                _interpolationStates[packet.DupeName] = interpState;
            }

            // Calculate velocity for prediction
            Vector2 newPos = new Vector2(packet.PosX, packet.PosY);
            if (interpState.StateBuffer.Count > 0)
            {
                var lastState = interpState.StateBuffer[interpState.StateBuffer.Count - 1];
                float timeDelta = packet.Timestamp - lastState.Timestamp;
                if (timeDelta > 0.001f)
                {
                    interpState.Velocity = (newPos - lastState.Position) / timeDelta;
                }
            }

            // Add to state buffer
            interpState.StateBuffer.Add(new BufferedState
            {
                Position = newPos,
                Timestamp = packet.Timestamp,
                FacingRight = packet.FacingRight,
                AnimName = packet.AnimName,
                AnimMode = packet.AnimMode
            });

            // Keep buffer size limited
            while (interpState.StateBuffer.Count > StateBufferSize)
            {
                interpState.StateBuffer.RemoveAt(0);
            }

            // Check for animation change
            if (interpState.CurrentAnimName != packet.AnimName && !string.IsNullOrEmpty(packet.AnimName))
            {
                TryPlayAnimationOnClient(packet.DupeName, packet.AnimName, packet.AnimMode);
                interpState.CurrentAnimName = packet.AnimName;
            }

            // Update vitals immediately
            interpState.Stress = packet.Stress;
            interpState.Breath = packet.Breath;
            interpState.Calories = packet.Calories;
            interpState.Health = packet.Health;
            interpState.CurrentChoreId = packet.CurrentChoreId;
        }

        /// <summary>
        /// [CLIENT] Update interpolation for all dupes.
        /// Uses buffered states and velocity prediction for smooth movement.
        /// </summary>
        public void UpdateInterpolation(float deltaTime)
        {
            // Only run on clients who are in game
            if (!ClientMode.IsClient) return;
            if (!ClientMode.IsClientInGame) return;

            float currentTime = Time.time;

            foreach (var kvp in _interpolationStates)
            {
                string dupeName = kvp.Key;
                var state = kvp.Value;

                // Look up dupe by name (network-safe)
                var dupeObj = DupeOwnership.Instance?.GetDupeObjectByName(dupeName);
                if (dupeObj == null) continue;

                Vector2 interpolatedPos;
                bool facingRight = true;

                if (state.StateBuffer.Count >= 2)
                {
                    // Find two states to interpolate between
                    BufferedState from = state.StateBuffer[0];
                    BufferedState to = state.StateBuffer[state.StateBuffer.Count - 1];

                    // Calculate interpolation factor
                    float elapsed = (state.StateBuffer.Count - 1) * _averagePacketInterval;
                    state.InterpolationProgress += deltaTime;
                    
                    float t = Mathf.Clamp01(state.InterpolationProgress / Mathf.Max(elapsed, 0.01f));

                    // Smooth interpolation with easing
                    t = SmoothStep(t);
                    interpolatedPos = Vector2.Lerp(from.Position, to.Position, t);
                    facingRight = t > 0.5f ? to.FacingRight : from.FacingRight;

                    // If we've caught up, add prediction
                    if (t >= 1.0f && state.Velocity.sqrMagnitude > 0.01f)
                    {
                        float extrapolateTime = Mathf.Min(state.InterpolationProgress - elapsed, MaxExtrapolation);
                        if (extrapolateTime > 0)
                        {
                            interpolatedPos += state.Velocity * extrapolateTime;
                        }
                    }

                    // Reset progress when we receive new data
                    if (state.InterpolationProgress > elapsed + _averagePacketInterval)
                    {
                        state.InterpolationProgress = 0;
                    }
                }
                else if (state.StateBuffer.Count == 1)
                {
                    // Only one state - use it directly
                    interpolatedPos = state.StateBuffer[0].Position;
                    facingRight = state.StateBuffer[0].FacingRight;
                }
                else
                {
                    // No states yet
                    continue;
                }

                // Apply position
                var currentPos = dupeObj.transform.position;
                dupeObj.transform.position = new Vector3(interpolatedPos.x, interpolatedPos.y, currentPos.z);

                // Apply facing
                var facing = dupeObj.GetComponent<Facing>();
                if (facing != null)
                {
                    facing.SetFacing(facingRight);
                }
            }
        }

        /// <summary>
        /// Smooth step function for better interpolation feel.
        /// </summary>
        private float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// [CLIENT] Play animation on client dupe by name.
        /// </summary>
        private void TryPlayAnimationOnClient(string dupeName, string animName, KAnim.PlayMode mode)
        {
            if (string.IsNullOrEmpty(animName)) return;

            var dupeObj = DupeOwnership.Instance?.GetDupeObjectByName(dupeName);
            if (dupeObj == null) return;

            var animController = dupeObj.GetComponent<KBatchedAnimController>();
            if (animController == null) return;

            try
            {
                animController.Play(animName, mode, 1f, 0f);
            }
            catch
            {
                // Don't spam logs - animation might not exist on client
            }
        }

        public void Clear()
        {
            _interpolationStates.Clear();
        }

        /// <summary>
        /// Buffered state for interpolation.
        /// </summary>
        private class BufferedState
        {
            public Vector2 Position;
            public float Timestamp;
            public bool FacingRight;
            public string AnimName;
            public KAnim.PlayMode AnimMode;
        }

        /// <summary>
        /// Per-dupe interpolation state.
        /// </summary>
        private class DupeInterpolationState
        {
            public List<BufferedState> StateBuffer = new List<BufferedState>();
            public Vector2 Velocity;
            public float InterpolationProgress;
            public string CurrentAnimName;
            public int CurrentChoreId;
            public float Stress, Breath, Calories, Health;
        }
    }
}