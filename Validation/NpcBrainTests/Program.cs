using System;
using System.Collections.Generic;
using IntercomMvp;

internal static class Program
{
    private static int checks;
    private static readonly IntercomNpcFacts Quiet = Facts(alive:false);
    private static readonly IntercomNpcFacts Seen = Facts();
    private static readonly IntercomNpcFacts Near = Facts(attack:true);
    private static void Check(bool condition, string label)
    { if(!condition) throw new Exception(label); checks++; }
    private static IntercomNpcFacts Facts(bool alive=true, bool detected=true, bool visible=true,
        bool attack=false, bool outside=false, bool home=false, bool arrived=false, bool search=false)
        =>new IntercomNpcFacts(alive,detected,visible,attack,outside,home,arrived,search);
    private static IntercomNpcBrain Brain()
    { var brain=new IntercomNpcBrain(); brain.Configure(new IntercomNpcRules(.45f,.65f,2.5f,1.1f)); return brain; }
    private static void Advance(IntercomNpcBrain brain,float seconds,IntercomNpcFacts facts,float dt=.01f)
    { for(int i=0;i<(int)Math.Ceiling(seconds/dt);i++) brain.Tick(dt,facts); }
    private static IntercomNpcBrain Windup()
    {
        var brain=Brain();
        Check(!brain.Tick(.01f,Seen)&&brain.Phase==IntercomNpcPhase.Chase,"detection starts chase without instant attack");
        Check(!brain.Tick(.01f,Near)&&brain.Phase==IntercomNpcPhase.Windup,"close visible target starts windup without hit");
        return brain;
    }
    private static void Main()
    {
        PatrolAndAwareness(); SearchOrders(); AttackAndRecovery(); CancellationAndDeath(); FrameTiming(); Geometry(); Tuning();
        Console.WriteLine($"NPC brain and melee geometry: {checks} assertions passed. Pure production logic; no Unity physics or rendering.");
    }
    private static void PatrolAndAwareness()
    {
        var brain=Brain();
        Check(brain.Phase==IntercomNpcPhase.Idle,"new enemy is idle");
        Advance(brain,1f,Quiet);Check(brain.Phase==IntercomNpcPhase.Idle,"idle pause remains visible");
        Advance(brain,.2f,Quiet);Check(brain.Phase==IntercomNpcPhase.Patrol,"idle proceeds to patrol");
        brain.Tick(.01f,Facts(alive:false,arrived:true));Check(brain.Phase==IntercomNpcPhase.Idle,"reached patrol endpoint rests");
        Advance(brain,1.2f,Quiet);Advance(brain,8.1f,Quiet);Check(brain.Phase==IntercomNpcPhase.Idle,"unreachable patrol endpoint cannot trap patrol forever");
        brain.Tick(.01f,Facts(detected:false,visible:false));Check(brain.Phase==IntercomNpcPhase.Idle,"unseen distant player is not acquired");
        brain.Tick(.01f,Seen);Check(brain.Phase==IntercomNpcPhase.Chase,"visible nearby player is acquired");
        Advance(brain,2.3f,Facts(detected:false,visible:false));Check(brain.Phase==IntercomNpcPhase.Chase,"last known target retained briefly");
        brain.Tick(.01f,Seen);
        Advance(brain,2.3f,Facts(detected:false,visible:false));Check(brain.Phase==IntercomNpcPhase.Chase,"seeing target refreshes memory");
        Advance(brain,.3f,Facts(detected:false,visible:false));Check(brain.Phase==IntercomNpcPhase.Return,"lost target eventually returns home");
        brain.Tick(.1f,Facts(outside:true));Check(brain.Phase==IntercomNpcPhase.Return,"outside-leash target cannot cause return/chase oscillation");
        brain.Tick(.1f,Near);Check(brain.Phase==IntercomNpcPhase.Chase,"returning robot reacquires a newly visible nearby victim inside its leash");
        brain.Tick(.01f,Facts(outside:true));
        brain.Tick(.01f,Facts(detected:false,visible:false,home:true));Check(brain.Phase==IntercomNpcPhase.Idle,"home arrival without visible victim rests");
        brain.Tick(.01f,Seen);Check(brain.Phase==IntercomNpcPhase.Chase,"can reacquire after returning home");
        brain.Tick(.01f,Facts(outside:true));Check(brain.Phase==IntercomNpcPhase.Return,"leash cancels pursuit");
        brain.Reset();brain.Alert();Check(brain.Phase==IntercomNpcPhase.Chase,"shot alert acquires even before ordinary detection");
        brain.ReturnHome();Check(brain.Phase==IntercomNpcPhase.Return,"explicit stuck-path recovery can return");
    }
    private static void SearchOrders()
    {
        var brain=Brain();brain.Alert();
        Advance(brain,4f,Facts(detected:false,visible:false,search:true));
        Check(brain.Phase==IntercomNpcPhase.Chase,"known-location order keeps investigation active beyond ordinary sight memory");
        Advance(brain,3f,Facts(detected:false,visible:false));
        Check(brain.Phase==IntercomNpcPhase.Return,"expired search order returns to ordinary bounded memory");
        brain.Reset();brain.Alert();brain.Tick(.01f,Facts(alive:false,search:true));
        Check(brain.Phase==IntercomNpcPhase.Return,"search order cannot keep fighting a dead target");
    }
    private static void AttackAndRecovery()
    {
        var brain=Windup();
        for(int i=0;i<44;i++) Check(!brain.Tick(.01f,Near),"no damage event before .45-second windup");
        Check(brain.Phase==IntercomNpcPhase.Windup,"windup remains active just before strike");
        float progress=brain.WindupProgress;brain.Alert();
        Check(brain.Phase==IntercomNpcPhase.Windup&&Math.Abs(brain.WindupProgress-progress)<.00001f,"incoming hit cannot reset or skip windup timer");
        Check(brain.Tick(.01f,Near),"one strike occurs at windup completion");
        Check(brain.Phase==IntercomNpcPhase.Recovery,"strike enters recovery");
        for(int i=0;i<64;i++) Check(!brain.Tick(.01f,Near),"recovery cannot deal additional contact damage");
        brain.Alert();Check(brain.Phase==IntercomNpcPhase.Recovery,"alert cannot interrupt recovery cooldown");
        Check(!brain.Tick(.01f,Near)&&brain.Phase==IntercomNpcPhase.Chase,"recovery completes without instant second attack");
        Check(!brain.Tick(.01f,Near)&&brain.Phase==IntercomNpcPhase.Windup,"next strike requires a fresh windup");

        brain=Brain();brain.Tick(.01f,Seen);
        for(int i=0;i<100;i++) Check(!brain.Tick(.01f,Seen)&&brain.Phase==IntercomNpcPhase.Chase,"visible target beyond reach is pursued without attacks");
        brain=Windup();
        for(int i=0;i<44;i++)brain.Tick(.01f,Facts(detected:false,visible:false));
        bool strike=brain.Tick(.01f,Facts(detected:false,visible:false));
        Check(strike,"windup can finish as an intentional miss after target moves");
        Check(!IntercomNpcCombatMath.InStrike(0,1,0,1,1.15f,40,false,true),"live line-of-sight test rejects a newly blocked strike event");
    }
    private static void CancellationAndDeath()
    {
        foreach(var cancel in new[]{Facts(alive:false,attack:true),Facts(outside:true,attack:true)})
        {
            var brain=Windup();Advance(brain,.44f,Near);
            Check(!brain.Tick(.1f,cancel)&&brain.Phase==IntercomNpcPhase.Return,"dead target or leash cancels pending lethal-frame strike");
        }
        var b=Windup();Advance(b,.2f,Near);float progress=b.WindupProgress;
        foreach(float dt in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            Check(!b.Tick(dt,Near)&&b.Phase==IntercomNpcPhase.Windup&&b.WindupProgress==progress,"invalid/paused time does not advance or hit");
        b.Interrupt();Check(b.Phase==IntercomNpcPhase.Idle&&b.WindupProgress==0,"focus/pause interruption cancels pending windup");
        b.Tick(.01f,Near);Check(b.Phase==IntercomNpcPhase.Chase,"resume requires new chase acquisition");
        b=Windup();b.Kill();
        for(int i=0;i<100;i++)
        {
            Check(!b.Tick(.1f,Near)&&b.Phase==IntercomNpcPhase.Dead,"dead enemy has no ticking attack");
            b.Alert();b.Interrupt();b.ReturnHome();Check(b.Phase==IntercomNpcPhase.Dead,"external alert/cancel/return cannot revive dead actor");
        }
        b.Reset();Check(b.Phase==IntercomNpcPhase.Idle,"explicit respawn resets death");
        b=Windup();b.Configure(new IntercomNpcRules(.7f,.8f,3f,2f));
        Check(b.Phase==IntercomNpcPhase.Idle&&b.WindupProgress==0,"profile configuration clears stale attack state");
    }
    private static void FrameTiming()
    {
        var totals=new List<int>();
        foreach(int fps in new[]{30,60,120})
        {
            var brain=Brain();double clock=0,windupStart=-1,lastHit=-100;int hits=0;
            float dt=1f/fps;
            for(int i=0;i<20*fps;i++)
            {
                var before=brain.Phase;bool strike=brain.Tick(dt,Near);clock+=dt;
                if(before!=IntercomNpcPhase.Windup&&brain.Phase==IntercomNpcPhase.Windup)windupStart=clock;
                if(strike)
                {
                    Check(clock-windupStart>=.45-.0001,"frame-rate-independent visible windup minimum");
                    Check(clock-lastHit>=1.1-.0001,"attack interval includes full windup and recovery");
                    lastHit=clock;hits++;
                }
            }
            totals.Add(hits);Check(hits>=16&&hits<=18,"bounded sustained strikes over twenty seconds");
        }
        Check(Math.Abs(totals[0]-totals[2])<=1,"30/120 FPS sustained cadence differs at most one strike over20s");
        var slow=Windup();
        for(int i=0;i<4;i++) Check(!slow.Tick(20f,Near),"huge frame cannot skip telegraph");
        Check(slow.Tick(20f,Near),"bounded time still eventually permits one strike");
        Check(!slow.Tick(20f,Near)&&slow.Phase==IntercomNpcPhase.Recovery,"huge frame cannot replay multiple attacks");
    }
    private static void Geometry()
    {
        bool Hit(float x,float z,float fx=0,float fz=1,bool clear=true,bool alive=true)
            =>IntercomNpcCombatMath.InStrike(x,z,fx,fz,1.15f,40,clear,alive);
        Check(Hit(0,1),"target directly ahead is hit");
        Check(Hit(0,1.15f),"reach boundary is included");
        Check(!Hit(0,1.151f),"outside reach misses");
        Check(!Hit(0,-.5f),"target behind locked heading misses");
        Check(!Hit(.8f,0),"lateral dodge leaves punch arc");
        Check(!Hit(0,1,clear:false),"wall blocks punch");
        Check(!Hit(0,1,alive:false),"dead player never receives punch");
        Check(!Hit(0,0,fx:0,fz:0),"invalid zero heading cannot deal damage");
        Check(Hit(0,0),"overlap in finite valid geometry accepted");
        for(int degrees=-180;degrees<=180;degrees++)
        {
            if(Math.Abs(degrees)==40)continue; // Float trig at the exact cone boundary is platform-sensitive.
            double angle=degrees*Math.PI/180d;
            bool hit=Hit((float)Math.Sin(angle),(float)Math.Cos(angle));
            Check(hit==(Math.Abs(degrees)<40),"angular sector matches locked punch heading");
        }
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            Check(!Hit(invalid,1)&&!Hit(0,invalid),"invalid displacement rejected");
            Check(!Hit(0,1,invalid,1)&&!Hit(0,1,0,invalid),"invalid heading rejected");
            Check(!IntercomNpcCombatMath.InStrike(0,1,0,1,invalid,40,true,true),"invalid reach rejected");
            Check(!IntercomNpcCombatMath.InStrike(0,1,0,1,2,invalid,true,true),"invalid angle rejected");
        }
        Check(!IntercomNpcCombatMath.InStrike(0,.1f,0,1,0,40,true,true),"zero reach rejected");
        Check(Hit(0,1,0,7),"heading magnitude does not change angle test");
        Check(!IntercomNpcCombatMath.InStrike(1e20f,0,.4e20f,1e20f,2e20f,40,true,true),"huge finite numerator cannot overflow into an inside-cone hit");
    }
    private static void Tuning()
    {
        var rules=new IntercomNpcRules(float.NaN,float.PositiveInfinity,-4,1000);
        Check(rules.Windup==.45f&&rules.Recovery==.65f&&rules.Memory==.1f&&rules.Idle==20f,"invalid tuning uses bounded defaults");
        var brain=Brain();brain.Configure(new IntercomNpcRules(.1f,.1f,.1f,.1f));
        Check(!brain.Tick(.1f,Near)&&brain.Phase==IntercomNpcPhase.Chase,"minimum timings still require detection");
        Check(!brain.Tick(.1f,Near)&&brain.Phase==IntercomNpcPhase.Windup,"minimum timings still require separate windup frame");
        Check(brain.Tick(.1f,Near)&&brain.Phase==IntercomNpcPhase.Recovery,"minimum finite windup strikes once");
        brain.Configure(default(IntercomNpcRules));brain.Tick(.01f,Near);brain.Tick(.01f,Near);
        Check(brain.Phase==IntercomNpcPhase.Windup&&!float.IsNaN(brain.WindupProgress),"default struct tuning is revalidated before progress division");
        Check(!brain.Tick(.01f,Near),"default struct cannot cause a zero-duration punch");
    }
}
