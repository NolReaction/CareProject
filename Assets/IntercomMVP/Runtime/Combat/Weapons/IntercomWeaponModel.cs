// Cohesive deterministic feature models; no scene components or Unity dependencies.
using System;
using System.Collections.Generic;

namespace IntercomMvp
{
    #region IntercomWeaponBalance
    /// <summary>Ideal balance with optional magazine/reload, first shot at t=0, all projectiles hit one target.</summary>
    public static class IntercomWeaponBalance
    {
        public static float Positive(float value, float fallback, float minimum = 0.01f)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(minimum, value);
        public static int VolleysToDefeat(float health, float damagePerProjectile, int projectilesPerVolley)
        {
            if (float.IsNaN(health) || float.IsInfinity(health) || health <= 0f) return 0;
            double damage = Positive(damagePerProjectile, 20f) * (double)Math.Max(1, projectilesPerVolley);
            return (int)Math.Min(int.MaxValue, Math.Ceiling(health / damage));
        }
        public static double IdealTtk(float health, float damage, int projectiles, float interval)
            => Math.Max(0, VolleysToDefeat(health, damage, projectiles) - 1) * (double)Positive(interval, 0.3f);
        public static double IdealTtkWithReload(float health, float damage, int projectiles, float interval, int magazine, float reload)
        {
            int volleys = VolleysToDefeat(health, damage, projectiles);
            int perMagazine = Math.Max(1, magazine / Math.Max(1, projectiles));
            int reloads = Math.Max(0, volleys - 1) / perMagazine;
            double gap = Positive(interval, 0.18f);
            // Auto reload begins on the last volley. Its duration overlaps the normal shot cooldown.
            return Math.Max(0, volleys - 1) * gap + reloads * Math.Max(0d, Positive(reload,1.15f,0.1f) - gap);
        }
        public static double SustainedDps(float damage, int projectiles, float interval, int magazine, float reload)
        {
            int perMagazine = Math.Max(1, magazine / Math.Max(1, projectiles));
            double cycle = (perMagazine - 1) * (double)Positive(interval,0.18f) + Math.Max(Positive(interval,0.18f),Positive(reload,1.15f,0.1f));
            return perMagazine * Math.Max(1,projectiles) * (double)Positive(damage,15f) / cycle;
        }
        public static double Dps(float damage, int projectiles, float interval)
            => Positive(damage, 20f) * (double)Math.Max(1, projectiles) / Positive(interval, 0.3f);
    }
    #endregion

    #region IntercomWeaponPoseClock
    public enum IntercomWeaponPosture { Relaxed, Raising, Combat, Lowering }

    /// <summary>Pure timing logic. No input, Animator, projectiles or Unity dependency.</summary>
    public sealed class IntercomWeaponPoseClock
    {
        public IntercomWeaponPosture Posture { get; private set; }
        public float RaiseStartNormalized { get; private set; }
        public bool ShotThisTick { get; private set; }
        public bool PostureChangedThisTick { get; private set; }

        private readonly double raiseDuration;
        private readonly double lowerDuration;
        private double shotInterval;
        private readonly double combatHold;
        private double now;
        private double phaseStart;
        private double phaseEnd;
        private double nextShot;
        private double holdUntil;
        private bool queuedShot;
        private bool continuousFire;

        public IntercomWeaponPoseClock(float raiseSeconds, float lowerSeconds, float fireInterval, float holdSeconds)
        {
            raiseDuration = Math.Max(0.001, raiseSeconds);
            lowerDuration = Math.Max(0.001, lowerSeconds);
            shotInterval = Math.Max(0.01, fireInterval);
            combatHold = Math.Max(0.0, holdSeconds);
            Reset();
        }

        /// <summary>Balance changes affect the next scheduled interval; an in-progress cooldown is retained.</summary>
        public void SetFireInterval(float seconds)
        {
            shotInterval = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0.18 : Math.Max(0.01, seconds);
        }

        public void Reset()
        {
            now = phaseStart = phaseEnd = nextShot = holdUntil = 0.0;
            queuedShot = continuousFire = ShotThisTick = PostureChangedThisTick = false;
            RaiseStartNormalized = 0f;
            Posture = IntercomWeaponPosture.Relaxed;
        }

        /// <summary>Queues one tap, including a tap released before the raise finishes.</summary>
        public void RequestFire() { queuedShot = true; }

        /// <summary>Drop fire intent while reloading without changing the current arm posture.</summary>
        public void CancelFireRequests() { queuedShot = continuousFire = ShotThisTick = false; }

        public void Tick(float deltaTime, bool triggerHeld, bool keepCombatPosture = false)
        {
            ShotThisTick = PostureChangedThisTick = false;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            now += deltaTime;
            if (!triggerHeld) continuousFire = false;

            bool wantsShot = queuedShot || triggerHeld;
            if (wantsShot && (Posture == IntercomWeaponPosture.Relaxed || Posture == IntercomWeaponPosture.Lowering))
            {
                // Lower and raise use the same endpoints. Reverse from the
                // matching point rather than dropping back to the lowered pose.
                RaiseStartNormalized = Posture == IntercomWeaponPosture.Lowering
                    ? (float)(1.0 - Clamp01((now - phaseStart) / lowerDuration)) : 0f;
                queuedShot = true;
                ChangePosture(IntercomWeaponPosture.Raising);
                phaseStart = now;
                phaseEnd = now + raiseDuration * (1.0 - RaiseStartNormalized);
            }

            if (Posture == IntercomWeaponPosture.Raising && now + 0.000001 >= phaseEnd)
                ChangePosture(IntercomWeaponPosture.Combat);

            if (Posture == IntercomWeaponPosture.Combat)
            {
                // Reload visuals need a stable raised baseline even for a designer's long reload.
                if (keepCombatPosture) holdUntil = now + combatHold;
                if ((queuedShot || triggerHeld) && now + 0.000001 >= nextShot)
                {
                    queuedShot = false;
                    ShotThisTick = true;
                    RegisterShot(triggerHeld && continuousFire);
                    continuousFire = triggerHeld;
                }
                else if (!keepCombatPosture && !queuedShot && !triggerHeld && now + 0.000001 >= holdUntil)
                {
                    ChangePosture(IntercomWeaponPosture.Lowering);
                    phaseStart = now;
                    phaseEnd = now + lowerDuration;
                }
            }

            if (Posture == IntercomWeaponPosture.Lowering && now + 0.000001 >= phaseEnd)
                ChangePosture(IntercomWeaponPosture.Relaxed);
        }

        /// <summary>For an external weapon that already fired while raised. Extends the hold timer.</summary>
        public bool NotifyShotFired()
        {
            if (Posture != IntercomWeaponPosture.Combat) return false;
            RegisterShot(false);
            continuousFire = false;
            return true;
        }

        private void RegisterShot(bool keepCadence)
        {
            // Held fire retains its deadline, so a non-integral frame interval
            // (for example .18 seconds at 30 FPS) does not drift on every shot.
            // If less than half an interval would remain, discard the debt
            // instead of compressing two reports into a burst after a hitch.
            // Taps and externally fired shots start a fresh interval.
            double scheduled = nextShot + shotInterval;
            nextShot = keepCadence && scheduled - now >= shotInterval * 0.5
                ? scheduled : now + shotInterval;
            holdUntil = now + combatHold;
        }

        private void ChangePosture(IntercomWeaponPosture value)
        {
            Posture = value;
            PostureChangedThisTick = true;
        }

        private static double Clamp01(double value) { return Math.Max(0.0, Math.Min(1.0, value)); }
    }
    #endregion

    #region IntercomMagazineState
    /// <summary>A finite magazine with unlimited reserve. No Unity, input or animation dependencies.</summary>
    public sealed class IntercomMagazineState
    {
        public int Capacity { get; private set; }
        public int CurrentAmmo { get; private set; }
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading ? (float)Math.Min(1d, elapsed / reloadDuration) : completed ? 1f : 0f;
        private double elapsed, reloadDuration;
        private bool completed;

        public IntercomMagazineState(int capacity) { Reset(capacity); }
        public void Reset(int capacity)
        {
            Capacity = Math.Max(1, Math.Min(999, capacity));
            CurrentAmmo = Capacity;
            IsReloading = completed = false;
            elapsed = 0d;
        }
        public bool CanFire(int projectiles) => !IsReloading && projectiles > 0 && CurrentAmmo >= projectiles;
        /// <summary>Call exactly once for the projectiles actually created. A failed/blocked volley consumes nothing.</summary>
        public bool TryConsume(int projectiles)
        {
            if (!CanFire(projectiles)) return false;
            CurrentAmmo -= projectiles;
            completed = false;
            return true;
        }
        public bool RequestReload(float seconds)
        {
            if (IsReloading || CurrentAmmo == Capacity) return false;
            reloadDuration = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 1.15d : Math.Max(0.1d, seconds);
            elapsed = 0d; completed = false; IsReloading = true;
            return true;
        }
        /// <returns>True once, on completion. No advance on pause or invalid time.</returns>
        public bool Tick(float deltaTime)
        {
            if (!IsReloading || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return false;
            elapsed += deltaTime;
            if (elapsed + 0.000001d < reloadDuration) return false;
            CurrentAmmo = Capacity; IsReloading = false; completed = true;
            return true;
        }
    }
    #endregion

    #region IntercomReloadControl
    /// <summary>Captures a reload-button pointer until release, preventing a UI tap from becoming fire.</summary>
    public sealed class IntercomReloadControl
    {
        private readonly HashSet<int> pointers = new HashSet<int>();
        private float minX, minY, maxX, maxY;
        public void Configure(float x, float y, float width, float height)
        { minX = x; minY = y; maxX = x + width; maxY = y + height; }
        public bool PointerDown(int id, float x, float y)
        {
            pointers.Remove(id);
            if (x < minX || x > maxX || y < minY || y > maxY) return false;
            pointers.Add(id); return true;
        }
        public bool Owns(int id) => pointers.Contains(id);
        public void PointerUp(int id) { pointers.Remove(id); }
        public void Reset() { pointers.Clear(); }
    }
    #endregion
}
