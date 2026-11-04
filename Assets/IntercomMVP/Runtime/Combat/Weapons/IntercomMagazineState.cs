using System;

namespace IntercomMvp
{
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
}
