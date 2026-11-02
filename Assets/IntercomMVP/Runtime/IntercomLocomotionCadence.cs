using System;

namespace IntercomMvp
{
    /// <summary>Keep normal analogue travel in the full-stride gait; blend idle only near a stop.</summary>
    public static class IntercomLocomotionCadence
    {
        public static float BlendWeight(float speed, float topSpeed, float fullStrideSpeed)
        {
            if (!Finite(speed) || speed <= 0f) return 0f;
            float maximum = Finite(topSpeed) ? Math.Max(0.1f, topSpeed) : 1.8f;
            float threshold = Finite(fullStrideSpeed) ? fullStrideSpeed : 0.45f;
            threshold = Math.Max(0.1f, Math.Min(maximum, threshold));
            return Math.Min(1f, speed / threshold);
        }

        public static float PlaybackRate(float speed, float referenceSpeed, float blendWeight)
        {
            if (!Finite(speed) || speed <= 0.01f) return 1f;
            float reference = Finite(referenceSpeed) ? Math.Max(0.1f, referenceSpeed) : 1.2f;
            float travelRate = Math.Max(0.1f, speed / reference);
            float weight = Finite(blendWeight) ? Math.Max(0f, Math.Min(1f, blendWeight)) : 0f;
            return 1f + (travelRate - 1f) * weight;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
