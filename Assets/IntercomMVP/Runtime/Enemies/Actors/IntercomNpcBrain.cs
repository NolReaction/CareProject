using System;

namespace IntercomMvp
{
    public enum IntercomNpcPhase { Idle, Patrol, Chase, Windup, Recovery, Return, Dead }

    public enum IntercomNpcArchetype { Standard, Runner, Heavy }

    /// <summary>Immutable multipliers on the designer's base profile; pooled lives never clone assets.</summary>
    public readonly struct IntercomNpcArchetypeTuning
    {
        public readonly IntercomNpcArchetype Archetype;
        public readonly float Health, Speed, Damage, Windup, Recovery, Scale;

        private IntercomNpcArchetypeTuning(IntercomNpcArchetype archetype, float health, float speed,
            float damage, float windup, float recovery, float scale)
        {
            Archetype=archetype; Health=health; Speed=speed; Damage=damage;
            Windup=windup; Recovery=recovery; Scale=scale;
        }

        public static IntercomNpcArchetypeTuning For(IntercomNpcArchetype archetype)
        {
            switch(archetype)
            {
                case IntercomNpcArchetype.Runner:
                    return new IntercomNpcArchetypeTuning(archetype,.60f,1.12f,.75f,1.10f,.90f,.90f);
                case IntercomNpcArchetype.Heavy:
                    return new IntercomNpcArchetypeTuning(archetype,1.80f,.72f,1.50f,1.40f,1.25f,1.12f);
                default:
                    return new IntercomNpcArchetypeTuning(IntercomNpcArchetype.Standard,1f,1f,1f,1f,1f,1f);
            }
        }
    }

    /// <summary>Allocation-free flat-plane crowd proposals. Runtime navigation still rejects blocked movement.</summary>
    public static class IntercomNpcCrowdMath
    {
        public const int MaximumSlots=8;

        public static int SlotForOrdinal(int ordinal)
        {
            int slot=ordinal%MaximumSlots;
            return slot<0 ? slot+MaximumSlots : slot;
        }

        /// <summary>Use the nearest physical side instead of assigning an intercepting far-side lane by spawn ordinal.</summary>
        public static int PreferredApproachSlot(float dx, float dz, int fallbackOrdinal)
        {
            if(!Finite(dx) || !Finite(dz) || (double)dx*dx+(double)dz*dz<.000001d)
                return SlotForOrdinal(fallbackOrdinal);
            double angle=Math.Atan2(dx,dz)*MaximumSlots/(Math.PI*2d);
            return SlotForOrdinal((int)Math.Round(angle,MidpointRounding.AwayFromZero));
        }

        public static void SlotOffset(int ordinal, float radius, out float x, out float z)
        {
            double r=Finite(radius) ? Math.Max(0d,Math.Min(4d,radius)) : 0d;
            double angle=SlotForOrdinal(ordinal)*Math.PI*2d/MaximumSlots;
            x=(float)(Math.Sin(angle)*r); z=(float)(Math.Cos(angle)*r);
        }

        /// <summary>dx/dz is self minus other. Swapping actors gives the opposite force, including coincident actors.</summary>
        public static void PairRepulsion(float dx, float dz, float spacing, int selfOrdinal, int otherOrdinal,
            out float x, out float z)
        {
            x=z=0f;
            if(!Finite(dx) || !Finite(dz) || !Finite(spacing) || spacing<=0f) return;
            double distanceSquared=(double)dx*dx+(double)dz*dz;
            double radius=Math.Max(.2d,Math.Min(3d,spacing));
            if(distanceSquared>=radius*radius) return;
            if(distanceSquared<.00000001d)
            {
                if(selfOrdinal==otherOrdinal) return;
                int low=Math.Min(selfOrdinal,otherOrdinal), high=Math.Max(selfOrdinal,otherOrdinal);
                uint seed;
                unchecked { seed=(uint)low*73856093u^(uint)high*19349663u; }
                double angle=(seed%65536u)*(Math.PI*2d/65536d);
                double sign=selfOrdinal<otherOrdinal ? 1d : -1d;
                x=(float)(Math.Sin(angle)*sign); z=(float)(Math.Cos(angle)*sign);
                return;
            }
            double distance=Math.Sqrt(distanceSquared), weight=1d-distance/radius;
            x=(float)(dx/distance*weight); z=(float)(dz/distance*weight);
        }

        /// <summary>dx/dz is other minus self. A neighbor behind or in another lane cannot brake this actor.</summary>
        public static float ForwardSpeedScale(float dx, float dz, float headingX, float headingZ, float spacing)
        {
            if(!Finite(dx)||!Finite(dz)||!Finite(headingX)||!Finite(headingZ)||!Finite(spacing)||spacing<=0f) return 0f;
            double headingSquared=(double)headingX*headingX+(double)headingZ*headingZ;
            if(headingSquared<.000001d) return 0f;
            double length=Math.Sqrt(headingSquared), hx=headingX/length, hz=headingZ/length;
            double forward=dx*hx+dz*hz, lateral=Math.Abs(dx*hz-dz*hx);
            double radius=Math.Max(.2d,Math.Min(3d,spacing));
            if(forward<=0d || lateral>=radius || forward>=radius*1.75d) return 1f;
            double proximity=Math.Max(0d,Math.Min(1d,(forward-radius*.60d)/(radius*1.15d)));
            // Blend out braking near the edge of a neighboring lane.
            double lane=Math.Max(0d,Math.Min(1d,lateral/radius));
            return (float)(proximity+(1d-proximity)*lane*lane);
        }

        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }

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
