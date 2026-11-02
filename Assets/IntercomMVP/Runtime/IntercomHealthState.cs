using System;
namespace IntercomMvp
{
    /// <summary>Pure health arithmetic, usable by enemies and destructible objects.</summary>
    public sealed class IntercomHealthState
    {
        public float Maximum { get; private set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public IntercomHealthState(float maximum) { Reset(maximum); }
        public void Reset(float maximum)
        { Maximum=IntercomWeaponBalance.Positive(maximum,100f,1f); Current=Maximum; }
        public bool Apply(float damage)
        {
            if(IsDead || damage<=0f || float.IsNaN(damage) || float.IsInfinity(damage))return false;
            Current=Math.Max(0f,Current-damage);return true;
        }
    }
}
