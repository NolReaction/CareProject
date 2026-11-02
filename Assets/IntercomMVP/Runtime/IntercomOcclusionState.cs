using System;

namespace IntercomMvp
{
    /// <summary>Acquisition/release hysteresis and frame-rate independent opacity.</summary>
    internal sealed class IntercomOcclusionState
    {
        internal bool Fading { get; private set; }
        internal float Opacity { get; private set; } = 1f;
        private float blockedSince = -1f, lastBlocked = float.NegativeInfinity;

        internal void Update(bool blocked, float now, float deltaTime, float obstructionDelay,
            float restoreDelay, float fadeSeconds, float ghostOpacity)
        {
            if (blocked)
            {
                lastBlocked = now;
                if (blockedSince < 0f) blockedSince = now;
                if (now - blockedSince >= Math.Max(0f, obstructionDelay)) Fading = true;
            }
            else
            {
                blockedSince = -1f;
                if (now - lastBlocked >= Math.Max(0f, restoreDelay)) Fading = false;
            }
            float desired = Fading ? Math.Max(0.1f, Math.Min(0.75f, ghostOpacity)) : 1f;
            float step = Math.Max(0f, deltaTime) / Math.Max(0.01f, fadeSeconds);
            Opacity = Opacity < desired ? Math.Min(desired, Opacity + step) : Math.Max(desired, Opacity - step);
        }

        internal void Reset()
        { Fading = false; Opacity = 1f; blockedSince = -1f; lastBlocked = float.NegativeInfinity; }
    }
}
