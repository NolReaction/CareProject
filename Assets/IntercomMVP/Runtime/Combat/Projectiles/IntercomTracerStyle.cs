using System;

namespace IntercomMvp
{
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
}
