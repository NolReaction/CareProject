using System;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IntercomMvp
{
    /// <summary>Upper-body animation preview and the future weapon handler's pose gate.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomWeaponPose : MonoBehaviour
    {
        public Animator animator;
        public IntercomWeaponPoseProfile profile;
        [Tooltip("Shared mobile/desktop input. When assigned, it replaces the legacy preview input below.")]
        public IntercomPlayerInput inputSource;
        [Tooltip("Mouse left / Space fallback used only when Input Source is empty.")]
        public bool previewInput = true;
        [Tooltip("Invoked after the arms are raised and the fire interval has elapsed. No bullets are created here.")]
        public UnityEvent onShotReady = new UnityEvent();

        /// <summary>Subscribe the future weapon to this event to create a projectile at the correct pose.</summary>
        public event Action ShotReady;
        /// <summary>Invalidates a ShotReady queued earlier in this frame, for example on respawn.</summary>
        public event Action FireCancelled;
        public IntercomWeaponPosture Posture => clock != null ? clock.Posture : IntercomWeaponPosture.Relaxed;
        public bool IsReadyToFire => Posture == IntercomWeaponPosture.Combat;

        private static readonly int RelaxedId = Animator.StringToHash("Arms.Relaxed");
        private static readonly int RaiseId = Animator.StringToHash("Arms.RaiseWeapons");
        private static readonly int CombatId = Animator.StringToHash("Arms.Combat");
        private static readonly int ShootId = Animator.StringToHash("Arms.Shoot");
        private static readonly int LowerId = Animator.StringToHash("Arms.LowerWeapons");
        private IntercomWeaponPoseClock clock;
        private RuntimeAnimatorController baseController;
        private int armsLayer = -1;
        private bool externalTriggerHeld;
        private bool pendingBeforeStart;
        private bool started;
        private bool hasFocus = true;
        private bool appPaused;
        private readonly IntercomInputCancellation inputCancellation = new IntercomInputCancellation();
        private float raiseSeconds;
        private float shotSeconds;
        private float recoilRemaining;

        private void Awake()
        {
            // Snapshot before Start so cancellation can also invalidate a tap
            // queued by another script while the Animator is still starting.
            ObserveInputCancellation();
        }

        private void Start()
        {
            started = true;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) baseController = animator.runtimeAnimatorController;
            Initialize();
        }

        private void OnEnable()
        {
            if (started) Initialize();
        }

        private void OnDisable()
        {
            ResetPosture();
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused) ResetPosture();
        }

        private void OnApplicationPause(bool paused)
        {
            appPaused = paused;
            if (paused) ResetPosture();
        }

        /// <summary>Cancel a queued tap and return to the relaxed pose after respawn or input cancellation.</summary>
        public void ResetPosture()
        {
            externalTriggerHeld = pendingBeforeStart = false;
            recoilRemaining = 0f;
            FireCancelled?.Invoke();
            if (clock == null) return;
            clock.Reset();
            if (animator != null && animator.isActiveAndEnabled && armsLayer >= 0)
                animator.Play(RelaxedId, armsLayer, BasePhase());
        }

        private void Initialize()
        {
            ObserveInputCancellation();
            if (animator == null || profile == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogError("Intercom MVP: подключи Animator и Weapon Pose Profile через Setup Movement + Combat.", this);
                enabled = false;
                return;
            }
            animator.runtimeAnimatorController = profile.animatorOverride != null ? profile.animatorOverride : baseController;
            armsLayer = animator.GetLayerIndex("Arms");
            if (armsLayer < 0 || !animator.HasState(armsLayer, RelaxedId) || !animator.HasState(armsLayer, RaiseId)
                || !animator.HasState(armsLayer, CombatId) || !animator.HasState(armsLayer, LowerId)
                || !animator.HasState(armsLayer, ShootId))
            {
                Debug.LogError("Intercom MVP: контроллеру нужны состояния слоя Arms. Запусти Setup Movement + Combat.", this);
                enabled = false;
                return;
            }
            raiseSeconds = ClipSeconds(profile.raiseWeapons, 8f / 24f);
            float lowerSeconds = ClipSeconds(profile.lowerWeapons, 12f / 24f);
            shotSeconds = ClipSeconds(profile.shoot, 9f / 24f);
            clock = new IntercomWeaponPoseClock(raiseSeconds, lowerSeconds, profile.fireInterval,
                Mathf.Max(profile.combatHold, shotSeconds + profile.poseBlend));
            recoilRemaining = 0f;
            animator.SetLayerWeight(armsLayer, 1f);
            animator.Play(RelaxedId, armsLayer, BasePhase());
            if (pendingBeforeStart) { clock.RequestFire(); pendingBeforeStart = false; }
        }

        private void Update()
        {
            // Input runs at -200 and can cancel while deltaTime is zero
            // (timeScale pause), on layout changes, or OS touch cancellation.
            // Consume that cancellation before any paused/Animator early exit.
            if (ObserveInputCancellation()) return;
            if (!hasFocus || appPaused || clock == null || animator == null || !animator.isActiveAndEnabled) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // Timings use clip seconds, so a globally slowed Animator also slows the pose gate.
            dt *= Mathf.Max(0f, animator.speed);
            if (dt <= 0f) return;
            bool held = externalTriggerHeld || (inputSource != null
                ? inputSource.FireHeld : previewInput && ReadPreviewHeld());
            bool pressed = inputSource != null
                ? inputSource.FirePressed : previewInput && PreviewPressed();
            if (pressed) clock.RequestFire();
            clock.Tick(dt, held);

            if (clock.PostureChangedThisTick)
            {
                recoilRemaining = 0f;
                switch (clock.Posture)
                {
                    case IntercomWeaponPosture.Raising:
                        animator.CrossFadeInFixedTime(RaiseId, profile.poseBlend, armsLayer, clock.RaiseStartNormalized * raiseSeconds);
                        break;
                    case IntercomWeaponPosture.Combat: BlendLocomotion(CombatId); break;
                    case IntercomWeaponPosture.Lowering:
                        animator.CrossFadeInFixedTime(LowerId, profile.poseBlend, armsLayer, 0f);
                        break;
                    default: BlendLocomotion(RelaxedId); break;
                }
            }

            if (clock.ShotThisTick)
            {
                PlayRecoil();
                ShotReady?.Invoke();
                onShotReady?.Invoke();
            }
            else if (recoilRemaining > 0f)
            {
                recoilRemaining -= dt;
                if (recoilRemaining <= 0f && clock.Posture == IntercomWeaponPosture.Combat)
                    BlendLocomotion(CombatId);
            }
        }

        private bool ObserveInputCancellation()
        {
            // Canonicalize a destroyed Unity object to null before identity
            // comparison, so removing/replacing the source also cancels taps.
            IntercomPlayerInput source = inputSource != null ? inputSource : null;
            uint version = source != null ? source.CancellationVersion : 0u;
            if (!inputCancellation.Observe(source, version)) return false;
            ResetPosture();
            return true;
        }

        public void RequestFire()
        {
            if (!hasFocus || appPaused) return;
            if (clock == null) pendingBeforeStart = true;
            else clock.RequestFire();
        }

        public void SetTriggerHeld(bool held) { externalTriggerHeld = held && hasFocus && !appPaused; }

        /// <summary>Alternative for an external firing scheduler; false while arms are not raised.
        /// Do not call this from ShotReady: that event already registered the shot.</summary>
        public bool NotifyShotFired()
        {
            if (!hasFocus || appPaused || clock == null || !clock.NotifyShotFired()) return false;
            PlayRecoil();
            return true;
        }

        /// <summary>Use another profile / AnimatorOverrideController without changing locomotion code.</summary>
        public void SetProfile(IntercomWeaponPoseProfile value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            FireCancelled?.Invoke();
            profile = value;
            externalTriggerHeld = pendingBeforeStart = false;
            if (started) Initialize();
        }

        private void PlayRecoil()
        {
            recoilRemaining = shotSeconds;
            animator.CrossFadeInFixedTime(ShootId, Mathf.Min(0.035f, profile.poseBlend), armsLayer, 0f);
        }

        private void BlendLocomotion(int stateId)
        {
            // Match the uninterrupted base layer's cycle when returning from a gesture.
            // A normalized offset stays correct even when RunRate or the
            // Idle/Run blend's duration changes during the return transition.
            float sourceDuration = Mathf.Max(0.001f, animator.GetCurrentAnimatorStateInfo(armsLayer).length);
            animator.CrossFade(stateId, profile.poseBlend / sourceDuration, armsLayer, BasePhase());
        }

        private float BasePhase() { return Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f); }
        private static float ClipSeconds(AnimationClip clip, float fallback) { return clip != null ? Mathf.Max(0.001f, clip.length) : fallback; }

        private static bool ReadPreviewHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.leftButton.isPressed)
                || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);
#else
            return false;
#endif
        }

        private static bool PreviewPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }
    }
}
