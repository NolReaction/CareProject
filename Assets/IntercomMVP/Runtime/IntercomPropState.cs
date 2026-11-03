using System;

namespace IntercomMvp
{
    // Stable values are used by DistrictLayout and the saved scene.
    public enum IntercomPropKind { Crate = 0, Bollard = 1, Relay = 2 }
    public enum IntercomPropPhase { Ready = 0, Damaged = 1, Spent = 2, Tripped = 3 }

    /// <summary>Small deterministic prop state machine. It owns no scene objects or physics.</summary>
    public sealed class IntercomPropState
    {
        public IntercomPropKind Kind { get; private set; }
        public IntercomPropPhase Phase { get; private set; }
        public float Health { get; private set; }
        public float MaximumHealth { get; private set; }
        public bool BlocksMovement => Phase != IntercomPropPhase.Spent;
        public float RecoveryProgress => Phase == IntercomPropPhase.Tripped
            ? (float)Math.Min(1d, elapsed / recoverySeconds) : 0f;
        private double elapsed, recoverySeconds;

        public void Reset(IntercomPropKind kind, float maximumHealth, float recoverSeconds)
        {
            Kind = Enum.IsDefined(typeof(IntercomPropKind), kind) ? kind : IntercomPropKind.Crate;
            MaximumHealth = Positive(maximumHealth, 45f, 1f);
            recoverySeconds = Positive(recoverSeconds, 4f, .25f);
            Health = MaximumHealth;
            Phase = IntercomPropPhase.Ready;
            elapsed = 0d;
        }

        public bool TakeDamage(float amount)
        {
            if (Phase == IntercomPropPhase.Spent || Phase == IntercomPropPhase.Tripped
                || !Finite(amount) || amount <= 0f) return false;
            Health = Math.Max(0f, Health - amount);
            if (Health > 0f) Phase = IntercomPropPhase.Damaged;
            else Phase = Kind == IntercomPropKind.Relay ? IntercomPropPhase.Tripped : IntercomPropPhase.Spent;
            elapsed = 0d;
            return true;
        }

        /// <returns>True exactly when a tripped relay becomes powered again.</returns>
        public bool Tick(float deltaTime)
        {
            if (Phase != IntercomPropPhase.Tripped || !Finite(deltaTime) || deltaTime <= 0f) return false;
            elapsed += deltaTime;
            if (elapsed + .000001d < recoverySeconds) return false;
            Health = MaximumHealth;
            Phase = IntercomPropPhase.Ready;
            elapsed = 0d;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Positive(float value, float fallback, float minimum)
            => Finite(value) ? Math.Max(minimum, value) : fallback;
    }
}
