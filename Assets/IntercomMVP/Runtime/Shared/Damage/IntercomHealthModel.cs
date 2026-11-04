// Cohesive deterministic feature models; no scene components or Unity dependencies.
using System;

namespace IntercomMvp
{
    #region IntercomHealthState
    /// <summary>Pure health arithmetic, usable by enemies and destructible objects.</summary>
    public sealed class IntercomHealthState
    {
        public float Maximum { get; private set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public IntercomHealthState(float maximum) { Reset(maximum); }
        public void Reset(float maximum)
        {
            // Health is shared by characters and props; it must not depend on
            // weapon balancing just to validate its maximum value.
            Maximum=float.IsNaN(maximum) || float.IsInfinity(maximum) ? 100f : Math.Max(1f,maximum);
            Current=Maximum;
        }
        public bool Apply(float damage)
        {
            if(IsDead || damage<=0f || float.IsNaN(damage) || float.IsInfinity(damage))return false;
            Current=Math.Max(0f,Current-damage);return true;
        }
    }
    #endregion

    #region IntercomTeam
    /// <summary>Neutral damage remains compatible with scenery and training targets.</summary>
    public enum IntercomTeam { Neutral = 0, Player = 1, Enemy = 2 }
    #endregion
}
