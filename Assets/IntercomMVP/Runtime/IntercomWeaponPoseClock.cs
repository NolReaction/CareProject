using System;

namespace IntercomMvp
{
    public enum IntercomWeaponPosture { Relaxed, Raising, Combat, Lowering }

    /// <summary>Pure timing logic. No input, Animator, projectiles or Unity dependency.</summary>
    public sealed class IntercomWeaponPoseClock
    {
        public IntercomWeaponPosture Posture { get; private set; }
        public float RaiseStartNormalized { get; private set; }
        public bool ShotThisTick { get; private set; }
        public bool PostureChangedThisTick { get; private set; }

        private readonly double raiseDuration;
        private readonly double lowerDuration;
        private double shotInterval;
        private readonly double combatHold;
        private double now;
        private double phaseStart;
        private double phaseEnd;
        private double nextShot;
        private double holdUntil;
        private bool queuedShot;
        private bool continuousFire;

        public IntercomWeaponPoseClock(float raiseSeconds, float lowerSeconds, float fireInterval, float holdSeconds)
        {
            raiseDuration = Math.Max(0.001, raiseSeconds);
            lowerDuration = Math.Max(0.001, lowerSeconds);
            shotInterval = Math.Max(0.01, fireInterval);
            combatHold = Math.Max(0.0, holdSeconds);
            Reset();
        }

        /// <summary>Balance changes affect the next scheduled interval; an in-progress cooldown is retained.</summary>
        public void SetFireInterval(float seconds)
        {
            shotInterval = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0.18 : Math.Max(0.01, seconds);
        }

        public void Reset()
        {
            now = phaseStart = phaseEnd = nextShot = holdUntil = 0.0;
            queuedShot = continuousFire = ShotThisTick = PostureChangedThisTick = false;
            RaiseStartNormalized = 0f;
            Posture = IntercomWeaponPosture.Relaxed;
        }

        /// <summary>Queues one tap, including a tap released before the raise finishes.</summary>
        public void RequestFire() { queuedShot = true; }

        public void Tick(float deltaTime, bool triggerHeld)
        {
            ShotThisTick = PostureChangedThisTick = false;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            now += deltaTime;
            if (!triggerHeld) continuousFire = false;

            bool wantsShot = queuedShot || triggerHeld;
            if (wantsShot && (Posture == IntercomWeaponPosture.Relaxed || Posture == IntercomWeaponPosture.Lowering))
            {
                // Lower and raise use the same endpoints. Reverse from the
                // matching point rather than dropping back to the lowered pose.
                RaiseStartNormalized = Posture == IntercomWeaponPosture.Lowering
                    ? (float)(1.0 - Clamp01((now - phaseStart) / lowerDuration)) : 0f;
                queuedShot = true;
                ChangePosture(IntercomWeaponPosture.Raising);
                phaseStart = now;
                phaseEnd = now + raiseDuration * (1.0 - RaiseStartNormalized);
            }

            if (Posture == IntercomWeaponPosture.Raising && now + 0.000001 >= phaseEnd)
                ChangePosture(IntercomWeaponPosture.Combat);

            if (Posture == IntercomWeaponPosture.Combat)
            {
                if ((queuedShot || triggerHeld) && now + 0.000001 >= nextShot)
                {
                    queuedShot = false;
                    ShotThisTick = true;
                    RegisterShot(triggerHeld && continuousFire);
                    continuousFire = triggerHeld;
                }
                else if (!queuedShot && !triggerHeld && now + 0.000001 >= holdUntil)
                {
                    ChangePosture(IntercomWeaponPosture.Lowering);
                    phaseStart = now;
                    phaseEnd = now + lowerDuration;
                }
            }

            if (Posture == IntercomWeaponPosture.Lowering && now + 0.000001 >= phaseEnd)
                ChangePosture(IntercomWeaponPosture.Relaxed);
        }

        /// <summary>For an external weapon that already fired while raised. Extends the hold timer.</summary>
        public bool NotifyShotFired()
        {
            if (Posture != IntercomWeaponPosture.Combat) return false;
            RegisterShot(false);
            continuousFire = false;
            return true;
        }

        private void RegisterShot(bool keepCadence)
        {
            // Held fire retains its deadline, so a non-integral frame interval
            // (for example .18 seconds at 30 FPS) does not drift on every shot.
            // If less than half an interval would remain, discard the debt
            // instead of compressing two reports into a burst after a hitch.
            // Taps and externally fired shots start a fresh interval.
            double scheduled = nextShot + shotInterval;
            nextShot = keepCadence && scheduled - now >= shotInterval * 0.5
                ? scheduled : now + shotInterval;
            holdUntil = now + combatHold;
        }

        private void ChangePosture(IntercomWeaponPosture value)
        {
            Posture = value;
            PostureChangedThisTick = true;
        }

        private static double Clamp01(double value) { return Math.Max(0.0, Math.Min(1.0, value)); }
    }
}
