using System;

namespace IntercomMvp
{
    /// <summary>One short punch with an opposite settling lobe, sampled from remaining time. No accumulated scaling.</summary>
    internal static class IntercomPropHitResponse
    {
        internal static float Duration(float value) => Finite(value) ? Math.Max(.08f, Math.Min(.6f, value)) : .26f;
        internal static float Shape(float remaining, float duration)
        {
            float t = Age(remaining, duration);
            return (float)(Math.Sin(t * Math.PI * 2d) * (1d - t));
        }
        internal static float Flash(float remaining, float duration)
        {
            float t = Age(remaining, duration);
            return (1f - t) * (1f - t);
        }
        private static float Age(float remaining, float duration)
        {
            if (!Finite(remaining) || remaining <= 0f) return 1f;
            return Math.Max(0f, Math.Min(1f, 1f - remaining / Duration(duration)));
        }
        internal static float Amount(float value, float fallback, float maximum)
            => Finite(value) ? Math.Max(0f, Math.Min(maximum, value)) : fallback;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
