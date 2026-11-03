using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Owns death/menu control locks and explicit spawn resets, not global pause.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class IntercomPlayerVitality : MonoBehaviour
    {
        public IntercomHealth health;
        public IntercomPlayerMotor motor;
        public IntercomPlayerInput input;
        public IntercomSimpleShooter shooter;
        public IntercomWeaponPose weaponPose;

        public IntercomHealth Health => health;
        public bool IsDead => health != null && health.IsDead;
        public bool MissionBlocked { get; private set; }
        public float ShowHitRemaining => hitRemaining;
        private IntercomHealth subscribedHealth;
        private IntercomPlayerMotor observedMotor;
        private IntercomPlayerMotor lockedMotor;
        private IntercomSimpleShooter lockedShooter;
        private IntercomWeaponPose lockedPose;
        private uint observedRespawn;
        private bool locked, motorWasEnabled, shooterWasEnabled, poseWasEnabled;
        private bool focused = true, paused;
        private float hitRemaining;

        private void Awake()
        {
            if(health == null) health = GetComponent<IntercomHealth>();
            if(motor == null) motor = GetComponent<IntercomPlayerMotor>();
            if(input == null) input = GetComponent<IntercomPlayerInput>();
            if(shooter == null) shooter = GetComponent<IntercomSimpleShooter>();
            if(weaponPose == null) weaponPose = GetComponent<IntercomWeaponPose>();
            observedMotor = motor;
            observedRespawn = motor != null ? motor.RespawnVersion : 0;
        }

        private void OnEnable()
        {
            Subscribe();
            if(IsDead || MissionBlocked) LockControls();
        }

        private void Update()
        {
            Subscribe();
            uint version = motor != null ? motor.RespawnVersion : 0;
            if(observedMotor != motor)
            {
                // Rebinding a reference is not an explicit respawn and must not heal.
                observedMotor = motor; observedRespawn = version;
            }
            else if(version != observedRespawn)
            {
                observedRespawn = version;
                if(health != null) health.ResetHealth();
                hitRemaining = 0f;
                if(!MissionBlocked) RestoreControls();
                if(input != null) input.CancelUntilRelease();
            }
            if(IsDead || MissionBlocked) LockControls();
            else RestoreControls();
            if(focused && !paused && Time.deltaTime > 0f)
                hitRemaining = Mathf.Max(0f, hitRemaining - Time.deltaTime);
        }

        private void Subscribe()
        {
            if(subscribedHealth == health) return;
            Unsubscribe();
            subscribedHealth = health;
            if(subscribedHealth == null) return;
            subscribedHealth.Damaged += OnDamaged;
            subscribedHealth.Died += OnDied;
        }

        private void Unsubscribe()
        {
            if(subscribedHealth != null)
            {
                subscribedHealth.Damaged -= OnDamaged;
                subscribedHealth.Died -= OnDied;
            }
            subscribedHealth = null;
        }

        private void OnDamaged(IntercomDamage damage) { hitRemaining = .25f; }
        private void OnDied() { if(IsDead) LockControls(); }

        /// <summary>Mission menus and results share the same component lock as death.</summary>
        public void SetMissionBlocked(bool blocked)
        {
            bool changed = MissionBlocked != blocked;
            MissionBlocked = blocked;
            if(IsDead || MissionBlocked) LockControls();
            else RestoreControls();
            if(changed && input != null) input.CancelUntilRelease();
        }

        private void LockControls()
        {
            if(locked && lockedMotor == motor && lockedShooter == shooter && lockedPose == weaponPose) return;
            if(locked) RestoreControls();
            locked = true;
            lockedMotor = motor; lockedShooter = shooter; lockedPose = weaponPose;
            motorWasEnabled = motor != null && motor.enabled;
            shooterWasEnabled = shooter != null && shooter.enabled;
            poseWasEnabled = weaponPose != null && weaponPose.enabled;
            if(input != null) input.CancelUntilRelease();
            if(shooter != null) shooter.enabled = false;
            if(weaponPose != null) weaponPose.enabled = false;
            if(motor != null)
            {
                motor.enabled = false;
                Animator animator = motor.animator;
                if(animator != null && animator.runtimeAnimatorController != null)
                {
                    animator.SetFloat("Speed", 0f);
                    animator.SetFloat("RunRate", 1f);
                }
            }
        }

        private void RestoreControls()
        {
            if(!locked) return;
            locked = false;
            if(lockedMotor != null) lockedMotor.enabled = motorWasEnabled;
            if(lockedPose != null) lockedPose.enabled = poseWasEnabled;
            if(lockedShooter != null) lockedShooter.enabled = shooterWasEnabled;
            lockedMotor = null; lockedShooter = null; lockedPose = null;
        }

        /// <summary>UI and keyboard retry route. The existing respawn version resets ammo, NPCs and props.</summary>
        public bool Retry()
        {
            if(!isActiveAndEnabled || !focused || paused || Time.timeScale <= 0f || motor == null || !Application.isPlaying) return false;
            uint before = motor.RespawnVersion;
            motor.ResetToSpawn();
            if(motor.RespawnVersion == before) return false;
            observedMotor = motor;
            observedRespawn = motor.RespawnVersion;
            if(health != null) health.ResetHealth();
            hitRemaining = 0f;
            if(IsDead || MissionBlocked) LockControls();
            else RestoreControls();
            if(input != null) input.CancelUntilRelease();
            return true;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if(input != null) input.CancelUntilRelease();
        }
        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            if(input != null) input.CancelUntilRelease();
        }
        private void OnDisable()
        {
            Unsubscribe();
            RestoreControls();
            if(input != null) input.CancelUntilRelease();
        }
    }
}
