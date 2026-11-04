using System;

namespace IntercomMvp
{
    public enum IntercomEnemySpawnKind { Perimeter, Ground, Hatch }

    /// <summary>Deterministic spawn cadence and bounded input checks. No camera, actor or random global state.</summary>
    public static class IntercomEnemySpawnPolicy
    {
        public static bool WantsVisible(int spawned, int every)
        {
            if (spawned < 0 || every <= 0) return false;
            int cadence = Math.Max(2, Math.Min(8, every));
            return ((long)spawned + 1L) % cadence == 0;
        }

        public static bool IsOnScreen(float x, float y, float depth, float width, float height)
        {
            if (!Finite(x) || !Finite(y) || !Finite(depth) || !Finite(width) || !Finite(height)
                || depth <= 0f || width <= 0f || height <= 0f) return false;
            // Keep a telegraphed arrival clear of both screen edges and HUD corners.
            return x >= width * .10f && x <= width * .90f && y >= height * .13f && y <= height * .87f;
        }

        public static bool DistanceSafe(float dx, float dz, float minimum)
        {
            if (!Finite(dx) || !Finite(dz) || !Finite(minimum) || minimum < 0f) return false;
            return (double)dx * dx + (double)dz * dz >= (double)minimum * minimum;
        }

        public static bool CooldownReady(float now, float readyAt)
            => Finite(now) && Finite(readyAt) && now >= readyAt;

        public static float NextReadyAt(float now, float cooldown)
        {
            if (!Finite(now)) return float.MaxValue;
            if (!Finite(cooldown)) cooldown = 4f;
            double next = (double)now + Math.Max(0d, Math.Min(60d, cooldown));
            return (float)Math.Max(-float.MaxValue, Math.Min(float.MaxValue, next));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
