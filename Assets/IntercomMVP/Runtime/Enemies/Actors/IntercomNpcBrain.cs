using System;

namespace IntercomMvp
{
    public enum IntercomNpcPhase { Idle, Patrol, Chase, Windup, Recovery, Return, Dead }

    public readonly struct IntercomNpcRules
    {
        public readonly float Windup, Recovery, Memory, Idle;
        public IntercomNpcRules(float windup, float recovery, float memory, float idle)
        {
            Windup = Safe(windup, .45f, .1f, 5f);
            Recovery = Safe(recovery, .65f, .1f, 8f);
            Memory = Safe(memory, 2.5f, .1f, 20f);
            Idle = Safe(idle, 1.1f, .1f, 20f);
        }
        private static float Safe(float value, float fallback, float min, float max)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
    }

    public readonly struct IntercomNpcFacts
    {
        public readonly bool TargetAlive, Detected, Visible, CanStartAttack, OutsideLeash, AtHome, AtPatrolGoal, HasSearchOrder;
        public IntercomNpcFacts(bool targetAlive, bool detected, bool visible, bool canStartAttack,
            bool outsideLeash, bool atHome, bool atPatrolGoal, bool hasSearchOrder = false)
        {
            TargetAlive=targetAlive; Detected=detected; Visible=visible; CanStartAttack=canStartAttack;
            OutsideLeash=outsideLeash; AtHome=atHome; AtPatrolGoal=atPatrolGoal;
            HasSearchOrder=hasSearchOrder;
        }
    }

    /// <summary>One telegraphed strike per attack. No damage, movement, physics or catch-up loop lives here.</summary>
    public sealed class IntercomNpcBrain
    {
        public IntercomNpcPhase Phase { get; private set; }
        public float WindupProgress => Phase == IntercomNpcPhase.Windup ? Math.Min(1f, elapsed / rules.Windup) : 0f;
        public float WindupRemaining => Phase == IntercomNpcPhase.Windup ? Math.Max(0f, rules.Windup-elapsed) : 0f;
        public float RecoveryProgress => Phase == IntercomNpcPhase.Recovery ? Math.Min(1f, elapsed / rules.Recovery) : 0f;
        private IntercomNpcRules rules = new IntercomNpcRules(.45f,.65f,2.5f,1.1f);
        private float elapsed, unseen;

        public void Configure(IntercomNpcRules tuning)
        { rules=new IntercomNpcRules(tuning.Windup,tuning.Recovery,tuning.Memory,tuning.Idle); Reset(); }
        public void Reset() { Phase=IntercomNpcPhase.Idle; elapsed=unseen=0f; }
        public void Kill() { Phase=IntercomNpcPhase.Dead; elapsed=0f; }
        public void Interrupt() { if(Phase!=IntercomNpcPhase.Dead) Reset(); }
        public void Alert()
        {
            if(Phase==IntercomNpcPhase.Dead) return;
            unseen=0f;
            if(Phase!=IntercomNpcPhase.Windup && Phase!=IntercomNpcPhase.Recovery) Enter(IntercomNpcPhase.Chase);
        }
        public void ReturnHome() { if(Phase!=IntercomNpcPhase.Dead) Enter(IntercomNpcPhase.Return); }

        /// <returns>True only for the single strike instant. Caller must recheck live geometry and target health.</returns>
        public bool Tick(float deltaTime, IntercomNpcFacts facts)
        {
            if(Phase==IntercomNpcPhase.Dead || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime<=0f) return false;
            // A resumed/slow frame must not skip a readable windup or replay attacks.
            float dt=Math.Min(.1f,deltaTime);
            elapsed+=dt;
            if(!facts.TargetAlive && (Phase==IntercomNpcPhase.Chase || Phase==IntercomNpcPhase.Windup || Phase==IntercomNpcPhase.Recovery))
            { Enter(IntercomNpcPhase.Return); return false; }
            if(facts.OutsideLeash && (Phase==IntercomNpcPhase.Chase || Phase==IntercomNpcPhase.Windup || Phase==IntercomNpcPhase.Recovery))
            { Enter(IntercomNpcPhase.Return); return false; }
            if(facts.TargetAlive && facts.Detected && !facts.OutsideLeash
                && (Phase==IntercomNpcPhase.Idle || Phase==IntercomNpcPhase.Patrol || Phase==IntercomNpcPhase.Return))
            { unseen=0f; Enter(IntercomNpcPhase.Chase); return false; }
            switch(Phase)
            {
                case IntercomNpcPhase.Idle:
                    if(elapsed>=rules.Idle) Enter(IntercomNpcPhase.Patrol);
                    break;
                case IntercomNpcPhase.Patrol:
                    if(facts.AtPatrolGoal || elapsed>=8f) Enter(IntercomNpcPhase.Idle);
                    break;
                case IntercomNpcPhase.Chase:
                    unseen=facts.Visible || facts.HasSearchOrder ? 0f : unseen+dt;
                    if(unseen>=rules.Memory) Enter(IntercomNpcPhase.Return);
                    else if(facts.CanStartAttack) Enter(IntercomNpcPhase.Windup);
                    break;
                case IntercomNpcPhase.Windup:
                    if(elapsed+.00001f>=rules.Windup)
                    { Enter(IntercomNpcPhase.Recovery); return true; }
                    break;
                case IntercomNpcPhase.Recovery:
                    if(elapsed+.00001f>=rules.Recovery) Enter(IntercomNpcPhase.Chase);
                    break;
                case IntercomNpcPhase.Return:
                    if(facts.AtHome) Enter(IntercomNpcPhase.Idle);
                    break;
            }
            return false;
        }
        private void Enter(IntercomNpcPhase phase) { Phase=phase; elapsed=0f; }
    }

    public static class IntercomNpcCombatMath
    {
        public static bool InStrike(float dx, float dz, float forwardX, float forwardZ,
            float range, float halfAngleDegrees, bool clear, bool targetAlive)
        {
            if(!clear || !targetAlive || !Finite(dx)||!Finite(dz)||!Finite(forwardX)||!Finite(forwardZ)
                || !Finite(range)||range<=0f||!Finite(halfAngleDegrees)) return false;
            double distanceSquared=(double)dx*dx+(double)dz*dz;
            double forwardSquared=(double)forwardX*forwardX+(double)forwardZ*forwardZ;
            if(distanceSquared>(double)range*range || forwardSquared<.000001d) return false;
            if(distanceSquared<.000001d) return true;
            double dot=((double)dx*forwardX+(double)dz*forwardZ)/Math.Sqrt(distanceSquared*forwardSquared);
            return dot>=Math.Cos(Math.Max(5d,Math.Min(85d,halfAngleDegrees))*Math.PI/180d);
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
