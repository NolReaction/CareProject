using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IntercomMvp
{
    /// <summary>Small in-place locomotion foundation. The wrapper moves; the FBX animates.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class IntercomPlayerMotor : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public Camera movementCamera;
        public IntercomPlayerInput inputSource;
        public IntercomWeaponPose weaponPose;

        [Header("Movement (world metres)")]
        [Min(0.1f)] public float moveSpeed = 0.8f;
        [Min(0.1f)] public float acceleration = 18f;
        [Min(0.1f)] public float deceleration = 24f;
        [Min(1f)] public float turnSpeed = 720f;
        public float gravity = -25f;
        [Tooltip("The imported robot faces local -Z. Change only if the model's forward axis changes.")]
        public float modelForwardYaw = 180f;

        [Header("Animation")]
        [Tooltip("World speed for which the authored Run clip was made.")]
        [Min(0.1f)] public float runReferenceSpeed = 0.8f;
        [Min(0f)] public float animationDamping = 0.08f;
        [Tooltip("Seconds for a smooth change of local running direction while aiming.")]
        [Min(0f)] public float directionDamping = 0.06f;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int RunRateId = Animator.StringToHash("RunRate");
        private static readonly int MoveXId = Animator.StringToHash("MoveX");
        private static readonly int MoveYId = Animator.StringToHash("MoveY");
        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private Vector3 lastAimDirection;
        private bool hasAim;
        private bool aiming;
        private IntercomPlayerInput observedInput;
        private uint inputCancellationSeen;
        private Vector2 localRunDirection = Vector2.up;
        private bool hadMovement;

        public bool IsAiming => aiming;
        public Vector3 FacingDirection => transform.TransformDirection(
            Quaternion.Euler(0f, modelForwardYaw, 0f) * Vector3.forward).normalized;
        public Vector3 AimWorldDirection => aiming && hasAim ? lastAimDirection : FacingDirection;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (movementCamera == null) movementCamera = Camera.main;
            if (inputSource == null) inputSource = GetComponent<IntercomPlayerInput>();
            if (weaponPose == null) weaponPose = GetComponent<IntercomWeaponPose>();
            if (animator != null) animator.applyRootMotion = false;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
#if !ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogWarning("Intercom MVP: ввод отключён. В Project Settings > Player > Active Input Handling выбери Input Manager (Old) или установи Input System и включи его.", this);
#endif
        }

        private void Update()
        {
            // Cancellation is distinct from an ordinary stick release. Clear
            // inertia even while timeScale is zero or a device rotates.
            if (observedInput != inputSource || (inputSource != null
                && inputCancellationSeen != inputSource.CancellationVersion))
            {
                observedInput = inputSource;
                inputCancellationSeen = inputSource != null ? inputSource.CancellationVersion : 0;
                horizontalVelocity = Vector3.zero;
                aiming = hasAim = false;
                hadMovement = false;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f || !controller.enabled) return;
            if ((inputSource != null ? inputSource.ResetPressed : ResetPressed()) || transform.position.y < spawnPosition.y - 10f)
            {
                ResetToSpawn();
                return;
            }

            Vector2 input = inputSource != null ? inputSource.Move : ReadKeyboard();
            Vector3 forward = movementCamera != null
                ? Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up).normalized
                : Vector3.forward;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
            Vector3 desiredVelocity = direction * moveSpeed;
            float change = direction.sqrMagnitude > 0f ? acceleration : deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, change * dt);

            bool liveAim = inputSource != null && inputSource.AimActive;
            if (liveAim)
            {
                Vector2 aim = inputSource.Aim;
                Vector3 desiredAim = right * aim.x + forward * aim.y;
                if (desiredAim.sqrMagnitude > 0.0001f)
                {
                    lastAimDirection = desiredAim.normalized;
                    hasAim = true;
                }
            }
            // Keep the requested heading while a short tap is waiting for the
            // hands to rise, and through the existing combat hold. Movement
            // remains independent: releasing the left stick never cancels aim.
            aiming = hasAim && (liveAim || (weaponPose != null
                && weaponPose.Posture != IntercomWeaponPosture.Relaxed));
            Vector3 facingDirection = aiming ? lastAimDirection : horizontalVelocity;
            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion facing = Quaternion.LookRotation(facingDirection, Vector3.up)
                    * Quaternion.Euler(0f, -modelForwardYaw, 0f);
                // The old static scene centred its wrapper on the silhouette,
                // including the guns. Turn about the actual body capsule so
                // the short legs do not orbit that forward-shifted pivot.
                Vector3 bodyPivot = transform.TransformPoint(controller.center);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, turnSpeed * dt);
                transform.position += bodyPivot - transform.TransformPoint(controller.center);
            }

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -40f);
            Vector3 before = transform.position;
            CollisionFlags collisions = controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            Vector3 actualVelocity = Vector3.ProjectOnPlane(transform.position - before, Vector3.up) / dt;
            float actualSpeed = actualVelocity.magnitude;
            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                // Project actual collision-resolved movement onto the visual
                // facing, not the Robot wrapper's +Z (this model faces -Z).
                Vector3 visualForward = FacingDirection;
                bool moving = IntercomLocomotionDirection.TryProject(actualVelocity.x, actualVelocity.z,
                    visualForward.x, visualForward.z, out float localX, out float localY);
                if (moving)
                {
                    if (!hadMovement) localRunDirection = new Vector2(localX, localY);
                    else
                    {
                        IntercomLocomotionDirection.Smooth(localRunDirection.x, localRunDirection.y, localX, localY,
                            dt, directionDamping, out float smoothX, out float smoothY);
                        localRunDirection = new Vector2(smoothX, smoothY);
                    }
                }
                hadMovement = moving;
                // Retain the last direction while Speed fades to idle. Sending
                // (0,0) would mix the four run clips during that fade.
                animator.SetFloat(MoveXId, localRunDirection.x);
                animator.SetFloat(MoveYId, localRunDirection.y);
                // Speed is measured after collision resolution: a wall stops the running pose.
                animator.SetFloat(SpeedId, Mathf.Clamp01(actualSpeed / Mathf.Max(0.1f, moveSpeed)), animationDamping, dt);
                float runRate = Mathf.Max(0.1f, moveSpeed / Mathf.Max(0.1f, runReferenceSpeed));
                animator.SetFloat(RunRateId, Mathf.Lerp(1f, runRate, animator.GetFloat(SpeedId)));
            }
        }

        [ContextMenu("Reset To Spawn (Play Mode)")]
        public void ResetToSpawn()
        {
            if (!Application.isPlaying || controller == null) return;
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            hasAim = aiming = false;
            if (inputSource != null) inputSource.CancelUntilRelease();
            if (weaponPose != null) weaponPose.ResetPosture();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(SpeedId, 0f);
                animator.SetFloat(MoveXId, 0f);
                animator.SetFloat(MoveYId, 1f);
            }
            localRunDirection = Vector2.up;
            hadMovement = false;
        }

        private void OnDisable()
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            aiming = hasAim = false;
            hadMovement = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { horizontalVelocity = Vector3.zero; aiming = hasAim = false; hadMovement = false; }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) { horizontalVelocity = Vector3.zero; aiming = hasAim = false; hadMovement = false; }
        }

        private static Vector2 ReadKeyboard()
        {
            Vector2 input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return input;
            input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
#elif ENABLE_LEGACY_INPUT_MANAGER
            input.x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            input.y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
#endif
            return Vector2.ClampMagnitude(input, 1f);
        }

        private static bool ResetPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }
    }
}
