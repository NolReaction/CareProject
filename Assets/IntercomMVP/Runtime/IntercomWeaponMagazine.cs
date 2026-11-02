using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IntercomMvp
{
    /// <summary>Magazine owner. Pose gates attempts; shooter commits only actual spawned bullets.</summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class IntercomWeaponMagazine : MonoBehaviour
    {
        public IntercomSimpleShooter shooter;
        public IntercomPlayerInput inputSource;
        public bool automaticReload = true;
        public event Action ReloadStarted;
        public event Action ReloadCompleted;
        public bool IsReloading => state != null && state.IsReloading;
        public float ReloadProgress => state != null ? state.ReloadProgress : 0f;
        public int CurrentAmmo => state != null ? state.CurrentAmmo : Capacity;
        public int Capacity => Definition != null ? Definition.MagazineCapacity : 24;
        public bool CanFire => isActiveAndEnabled && state != null && state.CanFire(ProjectilesPerVolley);
        private IntercomWeaponDefinition Definition => shooter != null ? shooter.Weapon : null;
        private int ProjectilesPerVolley => Definition != null ? Definition.ProjectilesPerVolley : 2;
        private float ReloadSeconds => Definition != null ? Definition.ReloadSeconds : 1.15f;
        private IntercomMagazineState state;
        private IntercomPlayerMotor motor;
        private IntercomWeaponDefinition loadedWeapon;
        private uint resetVersion;
        private bool focused = true, paused;

        private void Awake()
        {
            if (shooter == null) shooter = GetComponent<IntercomSimpleShooter>();
            if (inputSource == null) inputSource = GetComponent<IntercomPlayerInput>();
            motor = GetComponent<IntercomPlayerMotor>();
            resetVersion = motor != null ? motor.RespawnVersion : 0u;
            ResetMagazine();
        }
        private void OnEnable() { if (state == null) ResetMagazine(); }
        private void OnDisable() { ResetMagazine(); }
        private void OnApplicationFocus(bool value) { focused = value; }
        private void OnApplicationPause(bool value) { paused = value; }

        public void ResetMagazine()
        {
            state = new IntercomMagazineState(Capacity);
            loadedWeapon = Definition;
        }

        public bool RequestReload()
        {
            if (!isActiveAndEnabled || !focused || paused || Time.timeScale <= 0f || state == null) return false;
            if (!state.RequestReload(ReloadSeconds)) return false;
            ReloadStarted?.Invoke();
            return true;
        }

        /// <summary>Commit actual successful barrels once; invoke after spawning, never on an animation event.</summary>
        public bool CommitVolley(int successfulProjectiles)
        {
            if (state == null || !state.TryConsume(successfulProjectiles)) return false;
            if (automaticReload && !state.CanFire(ProjectilesPerVolley)) RequestReload();
            return true;
        }

        private void Update()
        {
            uint version = motor != null ? motor.RespawnVersion : 0u;
            if (version != resetVersion || loadedWeapon != Definition || state == null || state.Capacity != Capacity)
            {
                resetVersion = version;
                ResetMagazine();
            }
            if (!focused || paused || Time.timeScale <= 0f) return;
            if (state.Tick(Time.deltaTime)) ReloadCompleted?.Invoke();
            if (inputSource != null ? inputSource.ReloadPressed : ReadReloadPressed()) RequestReload();
            if (automaticReload && !state.IsReloading && !state.CanFire(ProjectilesPerVolley)) RequestReload();
        }

        private static bool ReadReloadPressed()
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
