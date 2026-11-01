using System;

namespace IntercomMvp
{
    /// <summary>Detects authored half-cycle contacts, never catching up several old steps after a stall.</summary>
    public sealed class IntercomFootstepClock
    {
        private bool initialized;
        private double previousPhase;
        private int previousState;

        public void Reset() { initialized = false; }

        public bool Tick(double normalizedTime, int state, double contactOffset, bool movingOnGround)
        {
            if (!movingOnGround || double.IsNaN(normalizedTime) || double.IsInfinity(normalizedTime)
                || double.IsNaN(contactOffset) || double.IsInfinity(contactOffset))
            {
                Reset();
                return false;
            }
            double phase = normalizedTime - contactOffset;
            if (!initialized || state != previousState)
            {
                previousPhase = phase;
                previousState = state;
                initialized = true;
                return false;
            }
            double advance = phase - previousPhase;
            bool crossed = Math.Floor(phase * 2.0) > Math.Floor(previousPhase * 2.0);
            previousPhase = phase;
            // Rewinds, controller resets and >half-cycle stalls resynchronize silently.
            return advance > 0.0 && advance <= 0.5 && crossed;
        }
    }
}
