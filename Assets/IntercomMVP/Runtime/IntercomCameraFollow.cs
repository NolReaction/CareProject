using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Authored camera framing with viewport freedom, damped catch-up and optional idle recentering.</summary>
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
        [Header("Movement inside the frame")]
        [Tooltip("The robot can move within a screen rectangle before the camera begins following.")]
        public bool freeMovement = true;
        [Tooltip("Full width and height as fractions of the viewport. 0.16 means 16% of the full screen width.")]
        public Vector2 deadZoneSize = new Vector2(0.16f, 0.12f);
        [Tooltip("Outer screen rectangle: catches unusually fast movement before the robot leaves the frame. Map bounds have priority.")]
        public Vector2 safetyZoneSize = new Vector2(0.65f, 0.55f);
        [Tooltip("Catch-up damping in seconds. Higher values feel heavier.")]
        [Min(0.01f)] public float smoothTime = 0.18f;
        [Tooltip("After the robot stops, gently restore the captured composition.")]
        public bool recenterWhenIdle = true;
        [Min(0f)] public float recenterDelay = 0.65f;
        [Min(0.01f)] public float recenterSmoothTime = 0.8f;
        [Min(0f)] public float idleSpeedThreshold = 0.04f;
        [Min(0.1f)] public float teleportDistance = 8f;
        public bool showTrackingGuides = true;
        [Header("Gentle anticipation")]
        [Range(0f, 1f)] public float lookAheadDistance = 0.28f;
        [Min(0.01f)] public float lookAheadSmoothTime = 0.14f;
        [Header("Optional map-edge framing")]
        [Tooltip("Optional map edge limit. It takes priority over the screen safety zone. Available for downward-facing orthographic views.")]
        public bool clampToGroundBounds;
        public Vector2 groundMinimum = new Vector2(-12f, -12f);
        public Vector2 groundMaximum = new Vector2(12f, 12f);
        [SerializeField, HideInInspector] private int presentationVersion;

        private Camera view;
        private CharacterController targetCapsule;
        private Transform cachedTarget;
        private Vector3 focus;
        private readonly IntercomCameraTracking tracking = new IntercomCameraTracking();
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
            focusHeight = 0.65f; framingOffset = Vector3.zero; smoothTime = 0.18f;
            lookAheadDistance = 0.28f; lookAheadSmoothTime = 0.14f;
            freeMovement = true; deadZoneSize = new Vector2(0.16f, 0.12f);
            safetyZoneSize = new Vector2(0.65f, 0.55f); recenterWhenIdle = true;
            recenterDelay = 0.65f; recenterSmoothTime = 0.8f;
            idleSpeedThreshold = 0.04f; teleportDistance = 8f;
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
            suppliedFrame = -10;
            ApplyLens();
            Vector3 anchor = TargetFocus;
            focus = ClampFocus(anchor);
            tracking.Reset(anchor.x, anchor.z);
            tracking.ApplyBounds(focus.x, focus.z, focus.x, focus.z);
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
            if (displacement.sqrMagnitude > Mathf.Max(0.1f, teleportDistance) * Mathf.Max(0.1f, teleportDistance)) { SnapToTarget(); return; }
            lastTargetPosition = point;
            Vector3 direction = Time.frameCount - suppliedFrame <= 1
                ? suppliedDirection : Vector3.ClampMagnitude(displacement / dt, 1f);
            Vector3 targetFocus = TargetFocus;
            tracking.Advance(targetFocus.x, targetFocus.z, direction.x, direction.z, dt,
                new IntercomCameraTracking.Settings
                {
                    freeMovement = freeMovement, recenterWhenIdle = recenterWhenIdle,
                    zoneWidth = deadZoneSize.x, zoneHeight = deadZoneSize.y,
                    safetyWidth = safetyZoneSize.x, safetyHeight = safetyZoneSize.y,
                    followSeconds = smoothTime, recenterDelay = recenterDelay,
                    recenterSeconds = recenterSmoothTime, idleSpeed = idleSpeedThreshold,
                    anticipationDistance = lookAheadDistance, anticipationSeconds = lookAheadSmoothTime,
                    teleportDistance = teleportDistance
                }, TrackingProjection());
            focus = ClampFocus(new Vector3((float)tracking.FocusX, targetFocus.y, (float)tracking.FocusZ));
            Vector3 anchor = ClampFocus(new Vector3((float)tracking.AnchorX, targetFocus.y, (float)tracking.AnchorZ));
            tracking.ApplyBounds(focus.x, focus.z, anchor.x, anchor.z);
            transform.position = focus - transform.forward * Mathf.Max(0.1f, distance);
        }

        private IntercomCameraTracking.Projection TrackingProjection()
        {
            Vector3 right = transform.right, up = transform.up, forward = transform.forward;
            float depth = Mathf.Max(0.1f, distance);
            return new IntercomCameraTracking.Projection
            {
                rightX = right.x, rightY = right.y, rightZ = right.z,
                upX = up.x, upY = up.y, upZ = up.z,
                forwardX = forward.x, forwardY = forward.y, forwardZ = forward.z,
                distance = depth, aspect = view.aspect, orthographic = orthographic,
                halfHeight = orthographic ? Mathf.Max(0.01f, orthographicSize)
                    : depth * Mathf.Tan(Mathf.Clamp(fieldOfView, 1f, 179f) * Mathf.Deg2Rad * 0.5f)
            };
        }

        private void OnValidate()
        {
            deadZoneSize = new Vector2(Mathf.Clamp(deadZoneSize.x, 0f, 0.8f), Mathf.Clamp(deadZoneSize.y, 0f, 0.8f));
            safetyZoneSize = new Vector2(Mathf.Clamp(safetyZoneSize.x, deadZoneSize.x, 0.95f), Mathf.Clamp(safetyZoneSize.y, deadZoneSize.y, 0.95f));
        }

        private void OnDrawGizmosSelected()
        {
            if (!showTrackingGuides || !freeMovement) return;
            Camera camera = GetComponent<Camera>();
            if (camera == null || camera.transform.forward.y >= -0.01f) return;
            DrawTrackingRectangle(camera, deadZoneSize, new Color(0.2f, 0.9f, 0.7f, 1f));
            DrawTrackingRectangle(camera, safetyZoneSize, new Color(1f, 0.65f, 0.15f, 0.8f));
        }

        private void DrawTrackingRectangle(Camera camera, Vector2 size, Color color)
        {
            Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight + focusHeight + framingOffset.y, 0f));
            Vector3[] corners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float x = (i == 0 || i == 3) ? -0.5f : 0.5f;
                float y = i < 2 ? -0.5f : 0.5f;
                Ray ray = camera.ViewportPointToRay(new Vector3(0.5f + x * size.x, 0.5f + y * size.y, 0f));
                if (!ground.Raycast(ray, out float enter)) return;
                corners[i] = ray.GetPoint(enter);
            }
            Gizmos.color = color;
            for (int i = 0; i < 4; i++) Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
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
