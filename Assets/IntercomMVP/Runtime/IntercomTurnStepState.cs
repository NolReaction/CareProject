using System;

namespace IntercomMvp
{
    /// <summary>
    /// Alternating stationary turn steps. The pose owner keeps the opposite boot
    /// planted and uses Phase for toe-off, swing and landing. No engine types.
    /// </summary>
    public sealed class IntercomTurnStepState
    {
        public float stepDuration = 0.18f;
        public float maxStepDegrees = 40f;
        public float startDegrees = 12f;
        public float finishDegrees = 3f;
        public float stationaryEnterSpeed = 0.12f;
        public float stationaryExitSpeed = 0.28f;

        public float LowerYaw { get; private set; }
        public float DestinationYaw => Active ? endYaw : LowerYaw;
        public float Phase { get; private set; }
        public bool SwingLeft { get; private set; }
        public int StepId { get; private set; }
        public bool Active { get; private set; }
        public bool Enabled { get; private set; }
        public bool StartedThisTick { get; private set; }
        public bool LandedThisTick { get; private set; }
        public bool IsGatherStep { get; private set; }
        public float LastStepYawDelta { get; private set; }

        private bool initialized, hasStepped, sequence, gatherPending;
        private float startYaw, endYaw, elapsed, duration, carry;

        public void Reset(float lowerYaw)
        {
            LowerYaw = Normalize(Finite(lowerYaw) ? lowerYaw : 0f);
            Phase = elapsed = carry = LastStepYawDelta = 0f;
            Active = Enabled = StartedThisTick = LandedThisTick = false;
            IsGatherStep = hasStepped = sequence = gatherPending = false;
            SwingLeft = false;
            StepId = 0;
            initialized = true;
        }

        /// <summary>
        /// Render the final landed pose when Active OR LandedThisTick. A new
        /// step never starts in the same tick as the previous landing, so the
        /// pose owner can commit the contact before switching support boots.
        /// </summary>
        public void Advance(float targetYaw, float actualSpeed, bool grounded, float dt)
        {
            StartedThisTick = LandedThisTick = false;
            if (!initialized) Reset(targetYaw);
            if (!Finite(targetYaw)) targetYaw = LowerYaw;
            targetYaw = Normalize(targetYaw);
            if (!grounded || !Finite(actualSpeed))
            {
                Cancel(targetYaw);
                return;
            }
            float exitSpeed = Clamp(Value(stationaryExitSpeed, 0.28f), 0.02f, 2f);
            float enterSpeed = Clamp(Value(stationaryEnterSpeed, 0.12f), 0f, exitSpeed * 0.95f);
            actualSpeed = Math.Max(0f, actualSpeed);
            if (actualSpeed >= exitSpeed)
            {
                Cancel(targetYaw);
                return;
            }
            if (!Enabled && actualSpeed <= enterSpeed) Enabled = true;
            if (!Enabled)
            {
                LowerYaw = targetYaw;
                return;
            }
            if (!Finite(dt) || dt <= 0f) return;

            if (!Active)
            {
                float error = DeltaAngle(LowerYaw, targetYaw);
                float start = Clamp(Value(startDegrees, 12f), 0.5f, 70f);
                float finish = Clamp(Value(finishDegrees, 3f), 0f, start * 0.75f);
                if (sequence && Math.Abs(error) > finish)
                    Begin(error, false);
                else if (sequence && gatherPending)
                    Begin(0f, true);
                else if (Math.Abs(error) >= start)
                    Begin(error, false);
                else
                {
                    carry = 0f;
                    sequence = false;
                    return;
                }
            }

            // Never skip several support transfers after a stalled frame.
            // Fractional remainder preserves average cadence across frame rates.
            elapsed += Math.Min(dt, 0.1f) + carry;
            carry = 0f;
            Phase = Clamp(elapsed / duration, 0f, 1f);
            float turn = Smooth((Phase - 0.12f) / (0.86f - 0.12f));
            LowerYaw = Normalize(startYaw + LastStepYawDelta * turn);
            if (Phase < 1f) return;

            LowerYaw = endYaw;
            carry = Clamp(elapsed - duration, 0f, 0.1f);
            Active = false;
            LandedThisTick = true;
            if (IsGatherStep)
            {
                gatherPending = sequence = false;
            }
            else gatherPending = true;
        }

        private void Begin(float error, bool gather)
        {
            SwingLeft = hasStepped ? !SwingLeft : error < 0f;
            hasStepped = sequence = Active = StartedThisTick = true;
            IsGatherStep = gather;
            duration = Clamp(Value(stepDuration, 0.18f), 0.08f, 0.8f);
            float limit = Clamp(Value(maxStepDegrees, 40f), 1f, 70f);
            LastStepYawDelta = gather ? 0f : Clamp(error, -limit, limit);
            startYaw = LowerYaw;
            endYaw = Normalize(startYaw + LastStepYawDelta);
            elapsed = Phase = 0f;
            unchecked { StepId++; }
        }

        private void Cancel(float targetYaw)
        {
            LowerYaw = targetYaw;
            Active = Enabled = sequence = gatherPending = false;
            StartedThisTick = LandedThisTick = IsGatherStep = false;
            Phase = elapsed = carry = LastStepYawDelta = 0f;
            // A new grounded sequence may choose the leading foot again.
            hasStepped = false;
        }

        /// <summary>Shortest signed yaw from fromYaw to toYaw, Unity's +Y convention.</summary>
        public static float DeltaAngle(float fromYaw, float toYaw)
        {
            if (!Finite(fromYaw) || !Finite(toYaw)) return 0f;
            double delta = ((double)toYaw - fromYaw) % 360.0;
            if (delta > 180.0) delta -= 360.0;
            if (delta <= -180.0) delta += 360.0;
            return (float)delta;
        }

        /// <summary>World X/Z body position after rotation about a fixed support point.</summary>
        public static void PlanarPivotOffset(float bodyX, float bodyZ, float pivotX, float pivotZ,
            float deltaDegrees, out float resultX, out float resultZ)
        {
            if (!Finite(bodyX) || !Finite(bodyZ) || !Finite(pivotX) || !Finite(pivotZ)
                || !Finite(deltaDegrees))
            {
                resultX = Finite(bodyX) ? bodyX : 0f;
                resultZ = Finite(bodyZ) ? bodyZ : 0f;
                return;
            }
            double radians = Normalize(deltaDegrees) * Math.PI / 180.0;
            double c = Math.Cos(radians), s = Math.Sin(radians);
            double x = (double)bodyX - pivotX, z = (double)bodyZ - pivotZ;
            resultX = (float)(pivotX + c * x + s * z);
            resultZ = (float)(pivotZ - s * x + c * z);
        }

        private static float Normalize(float yaw)
        {
            double result = yaw % 360.0;
            if (result > 180.0) result -= 360.0;
            if (result <= -180.0) result += 360.0;
            return (float)result;
        }
        private static float Smooth(float t) { t = Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }
        private static float Clamp(float x, float lo, float hi) => Math.Max(lo, Math.Min(hi, x));
        private static float Value(float x, float fallback) => Finite(x) ? x : fallback;
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
