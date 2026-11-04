using System;

namespace IntercomMvp
{
    /// <summary>Bounded, time-based chassis impulses. No bones, health or control state are changed here.</summary>
    public sealed class IntercomHurtResponse
    {
        private struct Pulse { internal float age, pitch, roll; internal bool active; }
        private readonly Pulse[] pulses = new Pulse[4];
        private int next;
        public float Pitch { get; private set; }
        public float Roll { get; private set; }
        public const float Duration = .48f;
        public const float MaximumPitch = 6f;
        public const float MaximumRoll = 4.5f;

        public void Push(float pitch, float roll)
        {
            if (!Finite(pitch) || !Finite(roll)) return;
            pulses[next] = new Pulse { pitch = Clamp(pitch, MaximumPitch), roll = Clamp(roll, MaximumRoll), active = true };
            next = (next + 1) % pulses.Length;
        }

        public void Advance(float seconds)
        {
            if (!Finite(seconds) || seconds < 0f) { Reset(); return; }
            float pitch = 0f, roll = 0f;
            for (int i = 0; i < pulses.Length; i++)
            {
                Pulse pulse = pulses[i];
                if (!pulse.active) continue;
                pulse.age += seconds;
                pulse.active = pulse.age < Duration;
                if (pulse.active)
                {
                    float envelope = Envelope(pulse.age);
                    pitch += pulse.pitch * envelope;
                    roll += pulse.roll * envelope;
                }
                pulses[i] = pulse;
            }
            Pitch = Clamp(pitch, MaximumPitch);
            Roll = Clamp(roll, MaximumRoll);
        }

        public void Reset()
        {
            for (int i = 0; i < pulses.Length; i++) pulses[i] = default;
            next = 0; Pitch = Roll = 0f;
        }

        public static float Envelope(float seconds)
        {
            if (!Finite(seconds) || seconds <= 0f || seconds >= Duration) return 0f;
            // A soft 45 ms take-up, followed by a restrained opposing spring return.
            // Time is sampled analytically: low frame rates cannot destabilize the spring.
            double first = seconds / .045;
            double recovery = Math.Max(0.0, (seconds - .12) / .075);
            double value = first * Math.Exp(1.0 - first) - .12 * recovery * Math.Exp(1.0 - recovery);
            double tail = Math.Min(1.0, (Duration - seconds) / .1);
            return (float)(value * tail * tail * (3.0 - 2.0 * tail));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value, float limit) => Math.Max(-limit, Math.Min(limit, value));
    }
}
