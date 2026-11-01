using System;

namespace IntercomMvp
{
    public static class IntercomGroundMath
    {
        public static float ContactAlpha(float height, float fadeHeight, float opacity, bool supported)
        {
            if (!supported || float.IsNaN(height) || float.IsInfinity(height)
                || float.IsNaN(fadeHeight) || float.IsInfinity(fadeHeight)
                || float.IsNaN(opacity) || float.IsInfinity(opacity)) return 0f;
            float weight = 1f - Math.Max(0f, height) / Math.Max(0.05f, fadeHeight);
            weight = Math.Max(0f, Math.Min(1f, weight));
            return Math.Max(0f, Math.Min(0.6f, opacity)) * weight * weight;
        }
    }
}
