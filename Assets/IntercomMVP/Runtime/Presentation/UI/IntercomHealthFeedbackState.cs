using System;

namespace IntercomMvp
{
    /// <summary>Scaled-time health presentation. Sampling twice at the same time never advances an animation.</summary>
    internal sealed class IntercomHealthFeedbackState
    {
        internal readonly struct Popup
        {
            public readonly string Text, ShortText;
            public readonly bool Healing;
            public readonly float Born;
            public Popup(float amount, float born)
            {
                Healing = amount > 0f;
                ShortText = (Healing ? "+" : "-") + Math.Ceiling(Math.Abs(amount)).ToString("0");
                Text = ShortText + " HP";
                Born = born;
            }
        }

        internal const int PopupCapacity = 4;
        internal const float PopupDuration = .95f;
        private readonly Popup[] popups = new Popup[PopupCapacity];
        private bool ready;
        private uint revision;
        private float lastSample, maximum, current, trailHoldUntil;
        private float damagedAt = -1000f, healedAt = -1000f;
        private int nextPopup;
        public float Clock { get; private set; }
        public float Fill { get; private set; }
        public float Trail { get; private set; }
        public float Target => maximum > 0f ? current / maximum : 0f;
        public bool Critical => current > 0f && Target <= .30f;
        public float DamagePulse => Decay(Clock - damagedAt, .48f);
        public float HealingPulse => Decay(Clock - healedAt, .85f);
        public float HealingProgress => Clamp((Clock - healedAt) / .65f);
        public float Shake => (float)Math.Sin((Clock - damagedAt) * 62f) * DamagePulse;
        public float CriticalPulse
        {
            get
            {
                if (!Critical) return 0f;
                float cycle = Clock % 1.35f;
                // A gentle two-beat power-core warning, never a full-screen strobe.
                return .12f + .55f * Bell(cycle - .16f, .085f) + .33f * Bell(cycle - .37f, .09f);
            }
        }

        public int LatestPopupIndex => (nextPopup + PopupCapacity - 1) % PopupCapacity;
        public Popup GetPopup(int index) => popups[index];
        public float PopupProgress(int index) => Clamp((Clock - popups[index].Born) / PopupDuration);
        public bool PopupVisible(int index) => popups[index].Text != null && Clock - popups[index].Born < PopupDuration;

        public void Reset(float value, float max, uint resetRevision, float now)
        {
            maximum = Finite(max) ? Math.Max(1f, max) : 100f;
            current = Finite(value) ? Math.Max(0f, Math.Min(maximum, value)) : maximum;
            lastSample = Finite(now) ? now : 0f;
            revision = resetRevision; ready = true;
            Clock = 0f; Fill = Trail = Target; trailHoldUntil = 0f;
            damagedAt = healedAt = -1000f; nextPopup = 0;
            Array.Clear(popups, 0, popups.Length);
        }

        public void Sample(float value, float max, uint resetRevision, float now, bool animate)
        {
            if (!Finite(value) || !Finite(max) || !Finite(now)) return;
            max = Math.Max(1f, max); value = Math.Max(0f, Math.Min(max, value));
            if (!ready || revision != resetRevision || max != maximum || now < lastSample)
            { Reset(value, max, resetRevision, now); return; }
            float previousClock = Clock;
            float elapsed = Math.Max(0f, now - lastSample);
            lastSample = now;
            if (!animate) return;
            Clock += elapsed;
            Fill = Ease(Fill, Target, elapsed, 16f);
            float trailElapsed = Math.Max(0f, Clock - Math.Max(previousClock, trailHoldUntil));
            Trail = Ease(Trail, Target, trailElapsed, 5.2f);
            if (value == current) return;
            float amount = value - current;
            if (amount < 0f)
            {
                Trail = Math.Max(Trail, Math.Max(Fill, Target));
                trailHoldUntil = Clock + .28f;
                damagedAt = Clock; healedAt = -1000f;
            }
            else
            {
                healedAt = Clock; damagedAt = -1000f;
                // Repair must never leave an old amber damage ghost above the new charge.
                Trail = Math.Min(Trail, value / maximum);
            }
            current = value;
            popups[nextPopup] = new Popup(amount, Clock);
            nextPopup = (nextPopup + 1) % PopupCapacity;
        }

        private static float Ease(float from, float to, float dt, float rate)
        {
            float value = to + (from - to) * (float)Math.Exp(-Math.Max(0f, dt) * rate);
            return Math.Abs(value - to) < .0002f ? to : Clamp(value);
        }
        private static float Decay(float age, float duration)
        { float value = 1f - Clamp(age / duration); return value * value; }
        private static float Bell(float value, float width) => (float)Math.Exp(-value * value / (width * width));
        private static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
