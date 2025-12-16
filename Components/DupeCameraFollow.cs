using UnityEngine;
using OniMultiplayer.Network;

namespace OniMultiplayer.Components
{
    /// <summary>
    /// Component that makes the camera follow the local player's dupe.
    /// Provides smooth camera tracking with optional toggle.
    /// </summary>
    public class DupeCameraFollow : MonoBehaviour
    {
        public static DupeCameraFollow Instance { get; private set; }

        private bool _isFollowing = true;
        private float _followSmoothTime = 0.3f;
        private Vector3 _velocity = Vector3.zero;
        private GameObject _targetDupe;

        public bool IsFollowing => _isFollowing;

        public static void Initialize()
        {
            if (Instance != null) return;

            var go = new GameObject("DupeCameraFollow");
            Instance = go.AddComponent<DupeCameraFollow>();
            DontDestroyOnLoad(go);

            OniMultiplayerMod.Log("DupeCameraFollow initialized");
        }

        private void Awake()
        {
            Instance = this;
        }

        private void LateUpdate()
        {
            if (!_isFollowing) return;
            if (!IsMultiplayer()) return;

            // Get our dupe
            int myDupeId = DupeOwnership.Instance?.GetLocalPlayerDupe() ?? -1;
            if (myDupeId < 0) return;

            // Cache the target dupe object
            if (_targetDupe == null || _targetDupe.GetInstanceID() != myDupeId)
            {
                _targetDupe = DupeOwnership.Instance?.GetDupeObject(myDupeId);
                if (_targetDupe == null)
                {
                    // Try to find it
                    _targetDupe = FindDupeById(myDupeId);
                }
            }

            if (_targetDupe == null) return;

            // Smooth follow the dupe
            FollowTarget(_targetDupe.transform.position);
        }

        private void FollowTarget(Vector3 targetPos)
        {
            if (CameraController.Instance == null) return;

            // Get the camera's current position (we only adjust X and Y, not Z/zoom)
            Vector3 currentPos = CameraController.Instance.transform.position;
            
            // Smooth the position
            Vector3 targetCamPos = new Vector3(targetPos.x, targetPos.y, currentPos.z);
            Vector3 smoothedPos = Vector3.SmoothDamp(currentPos, targetCamPos, ref _velocity, _followSmoothTime);

            // Set the camera position
            CameraController.Instance.SetTargetPos(new Vector3(smoothedPos.x, smoothedPos.y, 0), -1f, false);
        }

        private GameObject FindDupeById(int instanceId)
        {
            foreach (var minion in global::Components.LiveMinionIdentities.Items)
            {
                if (minion != null && minion.gameObject.GetInstanceID() == instanceId)
                {
                    return minion.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// Toggle camera follow on/off.
        /// </summary>
        public void ToggleFollow()
        {
            _isFollowing = !_isFollowing;
            OniMultiplayerMod.Log($"Camera follow: {(_isFollowing ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Enable camera follow.
        /// </summary>
        public void EnableFollow()
        {
            _isFollowing = true;
        }

        /// <summary>
        /// Disable camera follow.
        /// </summary>
        public void DisableFollow()
        {
            _isFollowing = false;
        }

        /// <summary>
        /// Snap camera to dupe instantly.
        /// </summary>
        public void SnapToMyDupe()
        {
            int myDupeId = DupeOwnership.Instance?.GetLocalPlayerDupe() ?? -1;
            if (myDupeId < 0) return;

            var dupeObj = DupeOwnership.Instance?.GetDupeObject(myDupeId);
            if (dupeObj == null)
            {
                dupeObj = FindDupeById(myDupeId);
            }

            if (dupeObj != null && CameraController.Instance != null)
            {
                CameraController.Instance.SetTargetPos(dupeObj.transform.position, 8f, true);
            }
        }

        private bool IsMultiplayer()
        {
            return SteamP2PManager.Instance?.IsConnected == true || 
                   SteamP2PManager.Instance?.IsHost == true;
        }
    }
}

