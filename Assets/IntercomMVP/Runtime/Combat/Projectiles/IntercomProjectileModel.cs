// Cohesive deterministic feature models; no scene components or Unity dependencies.
using System;

namespace IntercomMvp
{
    #region IntercomTracerStyle
    /// <summary>Visual dimensions only; these values never participate in collision or damage.</summary>
    public static class IntercomTracerStyle
    {
        public static float Width(float requested)
            => Finite(requested) ? Math.Max(0.03f, Math.Min(0.4f, requested)) : 0.15f;

        public static float Length(float requested, float travelled)
        {
            float maximum = Finite(requested) ? Math.Max(0.04f, Math.Min(2f, requested)) : 0.68f;
            float distance = Finite(travelled) ? Math.Max(0.04f, travelled) : 0.04f;
            // Grow out of the muzzle instead of showing a long streak through the robot on spawn.
            return Math.Min(maximum, distance);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #region IntercomProjectileHitChoice
    /// <summary>Select a closest valid hit from an unordered, complete physics result.</summary>
    public struct IntercomProjectileHitChoice
    {
        private float nearest;
        private readonly float maximum;
        public int Index { get; private set; }

        public IntercomProjectileHitChoice(float maximumDistance)
        {
            nearest = float.PositiveInfinity;
            maximum = maximumDistance;
            Index = -1;
        }

        public void Consider(int index, float distance, bool owner)
        {
            if (owner || index < 0 || float.IsNaN(distance) || float.IsInfinity(distance)
                || distance < 0f || distance > maximum || distance >= nearest) return;
            nearest = distance;
            Index = index;
        }

        public static bool IsComplete(int hitCount, int capacity) => capacity > 0 && hitCount >= 0 && hitCount < capacity;
    }
    #endregion
}
