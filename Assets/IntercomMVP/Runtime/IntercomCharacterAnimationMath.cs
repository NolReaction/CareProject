using System;

namespace IntercomMvp
{
    /// <summary>Bounded gesture envelopes; independent of the rendered frame rate.</summary>
    internal static class IntercomCharacterAnimationMath
    {
        internal static float Smooth(float value)
        {
            float t = Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        internal static float ReloadWeight(float progress)
        {
            if (!Finite(progress)) return 0f;
            return Smooth(progress / 0.20f) * (1f - Smooth((progress - 0.72f) / 0.28f));
        }

        internal static float ReloadLatch(float progress)
        {
            if (!Finite(progress)) return 0f;
            // A single mechanical wrist reset, never high-frequency vibration.
            return Smooth((progress - 0.27f) / 0.12f) * (1f - Smooth((progress - 0.48f) / 0.14f));
        }

        internal static float IdleInspection(float seconds)
        {
            if (!Finite(seconds) || seconds <= 0f) return 0f;
            float phase = seconds % 7.5f;
            return Smooth((phase - 3.5f) / 0.8f) * (1f - Smooth((phase - 4.7f) / 1.0f));
        }

        internal static float WallWeight(float distance, float near, float far)
        {
            if (!Finite(distance) || !Finite(near) || !Finite(far) || far <= near) return 0f;
            return 1f - Smooth((distance - near) / (far - near));
        }

        internal static float StrideSwing(float normalizedPhase)
        {
            if (!Finite(normalizedPhase)) return 0f;
            float phase = normalizedPhase - (float)Math.Floor(normalizedPhase);
            // Match the chassis weight-transfer phase. The hands lag by a
            // further .05 cycle instead of reversing rigidly with the hips.
            return (float)Math.Sin((phase - 0.11f) * 6.28318530718f);
        }

        internal static float Follow(float current, float target, float seconds, float deltaTime)
        {
            if (!Finite(current)) current = 0f;
            if (!Finite(target)) target = 0f;
            if (!Finite(deltaTime) || deltaTime <= 0f) return current;
            if (!Finite(seconds) || seconds <= 0f) return target;
            float change = deltaTime / seconds;
            return current < target ? Math.Min(target, current + change) : Math.Max(target, current - change);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp01(float value) => Finite(value) ? Math.Max(0f, Math.Min(1f, value)) : 0f;
    }
}
