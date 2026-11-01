using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Follows the gameplay capsule using an authored, capturable camera framing.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class IntercomCameraFollow : MonoBehaviour
    {
        public Transform target;
        [Header("Saved camera framing")]
        [Range(-89f, 89f)] public float pitch = 35f;
        public float yaw = 10f;
        public float roll;
        public bool orthographic = true;
        [Min(0.01f)] public float orthographicSize = 4.6f;
        [Range(1f, 179f)] public float fieldOfView = 60f;
        [Min(0.1f)] public float distance = 12f;
        public float groundHeight;
        [Min(0f)] public float focusHeight = 0.65f;
        [Tooltip("World-space offset of the framing centre from the target anchor. Capture buttons preserve your off-centre composition.")]
        public Vector3 framingOffset;
        [Min(0.01f)] public float smoothTime = 0.12f;
        [Header("Gentle anticipation")]
        [Range(0f, 1f)] public float lookAheadDistance = 0.35f;
        [Min(0.01f)] public float lookAheadSmoothTime = 0.18f;
        [Header("Optional map-edge framing")]
        [Tooltip("Normally off: keep the player near the authored screen position. Bounds clamping is available for downward-facing orthographic views.")]
        public bool clampToGroundBounds;
        public Vector2 groundMinimum = new Vector2(-12f, -12f);
        public Vector2 groundMaximum = new Vector2(12f, 12f);
        [SerializeField, HideInInspector] private int presentationVersion;

        private Camera view;
        private CharacterController targetCapsule;
        private Transform cachedTarget;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private Vector3 lookAhead;
        private Vector3 lookAheadVelocity;
        private Vector3 lastTargetPosition;
        private Vector3 suppliedDirection;
        private int suppliedFrame = -10;
        private bool initialized;

        private Vector3 TargetPosition => targetCapsule != null
            ? target.TransformPoint(targetCapsule.center) : target.position;
        private Quaternion SavedRotation => Quaternion.Euler(pitch, yaw, roll);
        private Vector3 TargetFocus => new Vector3(TargetPosition.x, groundHeight + focusHeight, TargetPosition.z) + framingOffset;

        /// <summary>New cameras use the lower 35-degree framing; existing authored settings are retained.</summary>
        public bool ApplyMobilePresetIfNeeded()
        {
            if (presentationVersion >= 1) return false;
            pitch = 35f; yaw = 10f; roll = 0f; orthographic = true;
            orthographicSize = 4.6f; fieldOfView = 60f; distance = 12f;
            focusHeight = 0.65f; framingOffset = Vector3.zero; smoothTime = 0.12f;
            lookAheadDistance = 0.35f; lookAheadSmoothTime = 0.18f;
            clampToGroundBounds = false;
            presentationVersion = 1;
            return true;
        }

        /// <summary>Prevents the one-time default preset from replacing explicitly authored fields.</summary>
        public void MarkFramingAuthored() { presentationVersion = 1; }

        /// <summary>
        /// Convert an actual camera pose into follow parameters without recentering the composition.
        /// Works with the game camera or an Editor Scene-view camera. The source camera is never changed.
        /// </summary>
        public bool CaptureFromCamera(Camera source)
        {
            if (source == null || target == null) return false;
            CacheTarget();
            Vector3 angles = source.transform.rotation.eulerAngles;
            pitch = SignedAngle(angles.x);
            yaw = SignedAngle(angles.y);
            roll = SignedAngle(angles.z);
            orthographic = source.orthographic;
            orthographicSize = Mathf.Max(0.01f, source.orthographicSize);
            fieldOfView = Mathf.Clamp(source.fieldOfView, 1f, 179f);
            Vector3 anchor = new Vector3(TargetPosition.x, groundHeight + focusHeight, TargetPosition.z);
            // A perpendicular offset plus forward distance reconstructs the full original position.
            // Even an off-centre or near-horizontal view is preserved rather than aimed at the robot.
            distance = Mathf.Max(0.1f, Vector3.Dot(anchor - source.transform.position, source.transform.forward));
            framingOffset = source.transform.position + source.transform.forward * distance - anchor;
            // A map-edge clamp would change a deliberately captured composition.
            clampToGroundBounds = false;
            presentationVersion = 1;
            initialized = false;
            return true;
        }

        /// <summary>Editor setup adopts a manually positioned camera before applying follow settings.</summary>
        public bool CaptureManualCameraChanges()
        {
            if (Application.isPlaying || target == null) return false;
            view = GetComponent<Camera>();
            CacheTarget();
            Quaternion rotation = SavedRotation;
            Vector3 expected = ClampFocus(TargetFocus, rotation) - rotation * Vector3.forward * Mathf.Max(0.1f, distance);
            bool moved = (transform.position - expected).sqrMagnitude > 0.000625f;
            bool turned = Quaternion.Angle(transform.rotation, rotation) > 0.1f;
            bool lensChanged = view.orthographic != orthographic
                || (orthographic ? Mathf.Abs(view.orthographicSize - orthographicSize) > 0.001f
                    : Mathf.Abs(view.fieldOfView - fieldOfView) > 0.001f);
            return (moved || turned || lensChanged) && CaptureFromCamera(view);
        }

        public void SetLookAheadDirection(Vector3 worldDirection)
        {
            suppliedDirection = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(worldDirection, Vector3.up), 1f);
            suppliedFrame = Time.frameCount;
        }

        private void OnEnable() { initialized = false; }
        private void Start() { SnapToTarget(); }

        [ContextMenu("Apply Saved Framing")]
        public void SnapToTarget()
        {
            if (target == null) return;
            view = GetComponent<Camera>();
            CacheTarget();
            lastTargetPosition = TargetPosition;
            lookAhead = lookAheadVelocity = focusVelocity = Vector3.zero;
            suppliedFrame = -10;
            ApplyLens();
            focus = ClampFocus(TargetFocus);
            transform.position = focus - transform.forward * Mathf.Max(0.1f, distance);
            initialized = true;
        }

        private void CacheTarget()
        {
            cachedTarget = target;
            targetCapsule = target != null ? target.GetComponent<CharacterController>() : null;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (!initialized || cachedTarget != target) { SnapToTarget(); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            ApplyLens();
            Vector3 point = TargetPosition;
            Vector3 displacement = Vector3.ProjectOnPlane(point - lastTargetPosition, Vector3.up);
            if (displacement.sqrMagnitude > 64f) { SnapToTarget(); return; }
            lastTargetPosition = point;
            Vector3 direction = Time.frameCount - suppliedFrame <= 1
                ? suppliedDirection : Vector3.ClampMagnitude(displacement / dt, 1f);
            lookAhead = Vector3.SmoothDamp(lookAhead, direction * lookAheadDistance,
                ref lookAheadVelocity, Mathf.Max(0.01f, lookAheadSmoothTime), Mathf.Infinity, dt);
            Vector3 desired = ClampFocus(TargetFocus + lookAhead);
            focus = Vector3.SmoothDamp(focus, desired, ref focusVelocity, Mathf.Max(0.01f, smoothTime), Mathf.Infinity, dt);
            focus = ClampFocus(focus);
            transform.position = focus - transform.forward * Mathf.Max(0.1f, distance);
        }

        private void ApplyLens()
        {
            if (view == null) view = GetComponent<Camera>();
            view.orthographic = orthographic;
            view.orthographicSize = Mathf.Max(0.01f, orthographicSize);
            view.fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
            transform.rotation = SavedRotation;
        }

        private Vector3 ClampFocus(Vector3 value)
        {
            return ClampFocus(value, transform.rotation);
        }

        private Vector3 ClampFocus(Vector3 value, Quaternion rotation)
        {
            if (!clampToGroundBounds || view == null || !orthographic) return value;
            Vector3 forward = rotation * Vector3.forward;
            if (forward.y >= -0.01f) return value;
            float halfHeight = Mathf.Max(0.01f, orthographicSize);
            float halfWidth = halfHeight * Mathf.Max(0.1f, view.aspect);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 origin = Vector3.up * (value.y - groundHeight) + (rotation * Vector3.right) * (x * halfWidth) + (rotation * Vector3.up) * (y * halfHeight);
                Vector3 ground = origin - forward * (origin.y / forward.y);
                min = Vector2.Min(min, new Vector2(ground.x, ground.z));
                max = Vector2.Max(max, new Vector2(ground.x, ground.z));
            }
            value.x = ClampOrCentre(value.x, groundMinimum.x - min.x, groundMaximum.x - max.x);
            value.z = ClampOrCentre(value.z, groundMinimum.y - min.y, groundMaximum.y - max.y);
            return value;
        }

        private static float SignedAngle(float value) { return value > 180f ? value - 360f : value; }
        private static float ClampOrCentre(float value, float low, float high)
        {
            return low <= high ? Mathf.Clamp(value, low, high) : (low + high) * 0.5f;
        }
    }
}
