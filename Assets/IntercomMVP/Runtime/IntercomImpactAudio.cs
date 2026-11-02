using System;

namespace IntercomMvp
{
    /// <summary>Material-local report suppression and readable arcade distance gain. No Unity dependency.</summary>
    public sealed class IntercomImpactAudio
    {
        private readonly double[] playedAt = new double[6];
        public IntercomImpactAudio() { Reset(); }

        public void Reset()
        {
            for (int i = 0; i < playedAt.Length; i++) playedAt[i] = double.NegativeInfinity;
        }

        public bool CanPlay(int material, double now, double interval)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return false;
            int index = Math.Max(0, Math.Min(playedAt.Length - 1, material));
            // Editor timeline resets and a changed clock must not lock reports out.
            if (now < playedAt[index]) playedAt[index] = double.NegativeInfinity;
            double delay = double.IsNaN(interval) || double.IsInfinity(interval) ? 0.035 : Math.Max(0.015, interval);
            return now - playedAt[index] + 1e-7 >= delay;
        }

        /// <summary>Only consume a slot after an audible clip was found.</summary>
        public void MarkPlayed(int material, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return;
            playedAt[Math.Max(0, Math.Min(playedAt.Length - 1, material))] = now;
        }

        public static float DistanceGain(float distance, float audibleRange)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance)) return 0f;
            double range = float.IsNaN(audibleRange) || float.IsInfinity(audibleRange) ? 32.0 : Math.Max(1.0, audibleRange);
            double t = Math.Max(0.0, distance) / range;
            if (t >= 1.0) return 0f;
            // Keep nearby material character audible; taper smoothly at the outer 20%.
            double gain = 1.0 / (1.0 + 1.6 * t * t);
            if (t > 0.8)
            {
                double fade = (t - 0.8) / 0.2;
                gain *= 1.0 - fade * fade * (3.0 - 2.0 * fade);
            }
            return (float)gain;
        }

        /// <summary>Briefly clear the gun tail for a real material hit, preserving the report attack.</summary>
        public static float ShotTailGain(float shotAge, float impactAge, float focusedGain)
        {
            if (float.IsNaN(shotAge) || float.IsNaN(impactAge) || float.IsInfinity(impactAge)
                || shotAge <= 0.015f || impactAge < 0f || impactAge >= 0.13f) return 1f;
            double target = float.IsNaN(focusedGain) || float.IsInfinity(focusedGain) ? 0.55 : Math.Max(0.2, Math.Min(1, focusedGain));
            double attack = Math.Max(0, Math.Min(1, (shotAge - 0.015) / 0.020));
            attack = attack * attack * (3 - 2 * attack);
            double release = Math.Max(0, Math.Min(1, (impactAge - 0.07) / 0.06));
            release = release * release * (3 - 2 * release);
            return (float)(1 - (1 - target) * attack * (1 - release));
        }
    }
}
