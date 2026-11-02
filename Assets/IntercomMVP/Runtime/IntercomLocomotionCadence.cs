using System;

namespace IntercomMvp
{
    /// <summary>Keep normal analogue travel in the full-stride gait; blend idle only near a stop.</summary>
    public static class IntercomLocomotionCadence
    {
        public static float BlendWeight(float speed, float topSpeed, float fullStrideSpeed)
        {
            if (!Finite(speed) || speed <= 0f) return 0f;
            float maximum = Finite(topSpeed) ? Math.Max(0.1f, topSpeed) : 2.1f;
            float threshold = Finite(fullStrideSpeed) ? fullStrideSpeed : 0.45f;
            threshold = Math.Max(0.1f, Math.Min(maximum, threshold));
            return Math.Min(1f, speed / threshold);
        }

        public static float PlaybackRate(float speed, float referenceSpeed, float blendWeight)
        {
            if (!Finite(speed) || speed <= 0.01f) return 1f;
            float reference = Finite(referenceSpeed) ? Math.Max(0.1f, referenceSpeed) : 1.0f;
            float travelRate = Math.Max(0.1f, speed / reference);
            float weight = Finite(blendWeight) ? Math.Max(0f, Math.Min(1f, blendWeight)) : 0f;
            return 1f + (travelRate - 1f) * weight;
        }

        // The lateral clips have a shorter physical stride. Map the requested
        // direction into their parameter space before the cardinal blend, then
        // use that same ellipse for speed and cadence.
        public static void AnimationDirection(float x, float y, float strafeRatio, out float animationX, out float animationY)
        {
            float side = SafeStrafeRatio(strafeRatio);
            x = Finite(x) ? x / side : 0f;
            y = Finite(y) ? y : 1f;
            float length = (float)Math.Sqrt(x * x + y * y);
            animationX = length > 0.0001f ? x / length : 0f;
            animationY = length > 0.0001f ? y / length : 1f;
        }

        public static float DirectionalReference(float animationX, float animationY, float referenceSpeed, float strafeRatio)
        {
            float reference = Finite(referenceSpeed) ? Math.Max(0.1f, referenceSpeed) : 1f;
            if (!Finite(animationX) || !Finite(animationY)) return reference;
            float length = (float)Math.Sqrt(animationX * animationX + animationY * animationY);
            if (length < 0.0001f) return reference;
            float x = animationX / length * SafeStrafeRatio(strafeRatio), y = animationY / length;
            return reference * (float)Math.Sqrt(x * x + y * y);
        }

        public static float SafeStrafeRatio(float ratio) => Finite(ratio) ? Math.Max(0.5f, Math.Min(1f, ratio)) : 0.75f;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
