using System;

namespace IntercomMvp
{
    public static class IntercomGroundMath
    {
        /// <summary>Support window shared by the authored 16-frame directional run clips.</summary>
        public static float StanceWeight(float normalizedPhase, bool left)
        {
            if (!Finite(normalizedPhase)) return 0f;
            float phase = normalizedPhase - (float)Math.Floor(normalizedPhase) - (left ? 0f : 0.5f);
            if (phase < 0f) phase += 1f;
            if (phase >= 0.32f) return 0f;
            // Let heel contact acquire gradually; release before toe-off.
            float enter = Smooth(phase / 0.04f);
            float leave = Smooth((0.32f - phase) / 0.05f);
            return enter * leave;
        }

        /// <summary>Friction lock only during flat support; heel strike and toe-off retain their roll.</summary>
        public static float PlantLockWeight(float normalizedPhase, bool left)
        {
            if (!Finite(normalizedPhase)) return 0f;
            float phase = normalizedPhase - (float)Math.Floor(normalizedPhase) - (left ? 0f : 0.5f);
            if (phase < 0f) phase += 1f;
            if (phase <= 0.10f || phase >= 0.22f) return 0f;
            return Smooth((phase - 0.10f) / 0.015f) * Smooth((0.22f - phase) / 0.015f);
        }

        /// <summary>
        /// Broad suspension pulse once per left/right stride. A short rigid leg
        /// already makes two quick contacts per cycle; copying both onto the
        /// shell reads as vibration. This slower, smooth bound gives the paired
        /// steps a heavy/light rhythm without changing either contact window.
        /// </summary>
        public static float GaitLift(float normalizedPhase)
        {
            if (!Finite(normalizedPhase)) return 0f;
            float phase = normalizedPhase - (float)Math.Floor(normalizedPhase);
            return 0.5f - 0.5f * (float)Math.Cos((phase - 0.12f) * 6.28318530718f);
        }

        /// <summary>Alternating shoulder/weight-transfer rhythm in [-1,1].</summary>
        public static float GaitCounterSwing(float normalizedPhase)
        {
            if (!Finite(normalizedPhase)) return 0f;
            float phase = normalizedPhase - (float)Math.Floor(normalizedPhase);
            return (float)Math.Sin((phase - 0.06f) * 6.28318530718f);
        }

        /// <summary>Height of a hip above an ankle for a single rigid calf link.</summary>
        public static bool TryHipHeight(float ankleY, float horizontalDistance, float linkLength, out float hipY)
        {
            hipY = ankleY;
            if (!Finite(ankleY) || !Finite(horizontalDistance) || !Finite(linkLength)
                || linkLength <= 0.001f || horizontalDistance < 0f
                || horizontalDistance >= linkLength * 0.985f) return false;
            hipY = ankleY + (float)Math.Sqrt(linkLength * linkLength - horizontalDistance * horizontalDistance);
            return true;
        }

        /// <summary>Exact critically damped spring step; no frame-rate-dependent Euler integration.</summary>
        public static void Spring(ref float position, ref float velocity, float target, float frequency, float dt)
        {
            if (!Finite(position) || !Finite(velocity) || !Finite(target)) { position = velocity = 0f; return; }
            if (!Finite(dt) || dt <= 0f) return;
            float omega = Math.Max(0.1f, Finite(frequency) ? frequency : 4.5f) * 6.28318530718f;
            float error = position - target;
            float tangent = velocity + omega * error;
            float decay = (float)Math.Exp(-omega * Math.Min(dt, 0.25f));
            float step = Math.Min(dt, 0.25f);
            position = target + (error + tangent * step) * decay;
            velocity = (velocity - omega * tangent * step) * decay;
        }

        private static float Smooth(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            return value * value * (3f - 2f * value);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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

    /// <summary>One impact per verified support window, with silent resynchronization.</summary>
    public sealed class IntercomFootPlantGate
    {
        private bool emitted;
        public bool Observe(bool inSupport, bool movingOnGround, bool touching, bool resync)
        {
            if (resync) { emitted = inSupport; return false; }
            if (!movingOnGround || !inSupport) { emitted = false; return false; }
            if (emitted || !touching) return false;
            emitted = true;
            return true;
        }
        public void Reset() { emitted = false; }
    }

}
