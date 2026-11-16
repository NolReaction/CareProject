using System;
using IntercomMvp;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label)
    { checks++; if(!value) throw new Exception(label); }
    private static void Main()
    {
        SelectionAndLimits(); ExactElimination(); PoolIdentity(); TerminalOutcomes(); Rescue(); FreePlay(); RandomizedAccounting();
        WaveRhythm(); ContinuousEscalation(); RunSummaries();
        Console.WriteLine($"Mission state: {checks} assertions passed. Production mission accounting; no Unity runtime or rendering.");
    }
    private static void FreePlay()
    {
        Check((int)IntercomMissionMode.Elimination==0 && (int)IntercomMissionMode.Rescue==1 && (int)IntercomMissionMode.FreePlay==2,
            "serialized modes remain stable");
        var state=new IntercomMissionState();state.Begin(IntercomMissionMode.FreePlay,500,500);
        Check(state.Mode==IntercomMissionMode.FreePlay && state.Phase==IntercomMissionPhase.Active,"free mode starts active");
        Check(state.Goal==0 && state.Supply==0,"free mode has neither kill objective nor enemy supply");
        for(int i=0;i<16;i++)Check(!state.RecordSpawn(i,1,state.Epoch),"free mode cannot record enemies");
        Check(!state.RecordKill(0,1,state.Epoch) && !state.CompleteRescue(true,true,true),"combat objectives cannot complete free mode");
        Check(state.Phase==IntercomMissionPhase.Active && state.Kills==0,"free mode stays open-ended");
        uint epoch=state.Epoch;state.Select();state.Begin(IntercomMissionMode.FreePlay,0,0);
        Check(state.Epoch!=epoch && state.Alive==0,"free retry isolates run epoch");
        state.Begin(IntercomMissionMode.Elimination,50);
        Check(state.Supply==50 && state.RecordSpawn(0,1,state.Epoch),"switching back to combat restores spawn accounting");
    }

    private static void WaveRhythm()
    {
        int remaining=50,total=0;int[] intended={6,8,10,12,14};
        for(int wave=1;wave<=5;wave++)
        {
            int release=Math.Min(remaining,IntercomMissionWavePolicy.WaveSize(10,wave,true));
            Check(release==intended[wave-1],"fifty-robot course escalates from a small readable introduction");
            remaining-=release;total+=release;
            Check(IntercomMissionWavePolicy.AliveLimit(6,wave,true)==Math.Min(6,wave+2),"active pressure grows gradually within authored cap");
        }
        Check(remaining==0&&total==50&&IntercomMissionWavePolicy.WaveCount(50,10,true)==5,"rhythm never invents or loses the goal supply");
        Check(IntercomMissionWavePolicy.WaveCount(30,10,true)==4,"rescue supply keeps a bounded short final group");
        for(int authored=-2;authored<=52;authored++)
            for(int supply=0;supply<=500;supply+=17)
            {
                int waves=IntercomMissionWavePolicy.WaveCount(supply,authored,true);
                int emitted=0;
                for(int w=1;w<=waves;w++)emitted+=Math.Min(supply-emitted,IntercomMissionWavePolicy.WaveSize(authored,w,true));
                Check(emitted==supply&&waves<=supply,"every custom positive supply terminates exactly, even tiny authored waves");
            }
        for(int w=1;w<=12;w++)
        {
            Check(IntercomMissionWavePolicy.WaveSize(10,w,false)==10&&IntercomMissionWavePolicy.AliveLimit(6,w,false)==6
                &&IntercomMissionWavePolicy.PaceMultiplier(w,false)==1f,"legacy profiles keep their exact wave and pacing contracts");
            int runners=0,heavies=0;
            for(int ordinal=0;ordinal<40;ordinal++)
            {
                int type=IntercomMissionWavePolicy.ArchetypeIndex(w,ordinal,true);
                Check(type>=0&&type<=2,"composition emits only supported archetypes");
                if(type==1)runners++;if(type==2)heavies++;
                Check(IntercomMissionWavePolicy.ArchetypeIndex(w,ordinal,false)==0,"legacy profiles remain standard-only");
            }
            Check(w==1 ? runners==0&&heavies==0 : w==2 ? runners>0&&heavies==0 : runners>0&&heavies>0,
                "fast enemies are introduced before heavy enemies");
            for(int cap=-2;cap<=10;cap++)Check(IntercomMissionWavePolicy.AliveLimit(cap,w,true)<=Math.Max(1,Math.Min(8,cap)),"rhythm cannot exceed designer concurrency");
        }
        Check(IntercomMissionWavePolicy.WaveSize(int.MaxValue,int.MinValue,true)==30,"invalid early wave cannot overflow into a finale");
        Check(IntercomMissionWavePolicy.WaveCount(int.MaxValue,0,true)==500,"invalid custom supply and wave size stay bounded");
        Check(IntercomMissionWavePolicy.WaveCount(-1,10,true)==0,"empty supply has no fake wave");
    }

    private static void ContinuousEscalation()
    {
        int remaining=50;int[] intended={10,14,18,8};
        for(int wave=1;wave<=4;wave++)
        {
            int quota=Math.Min(remaining,IntercomMissionWavePolicy.WaveSize(10,wave,true,true));
            Check(quota==intended[wave-1],"continuous quotas put more enemies in every complete wave");
            remaining-=quota;
            Check(IntercomMissionWavePolicy.AliveLimit(8,wave,true,true)==wave+4,"pressure grows from five through eight active enemies");
            Check(IntercomMissionWavePolicy.PaceMultiplier(wave,true,true)<=1,"continuous waves never slow the base spawn cadence");
        }
        Check(remaining==0&&IntercomMissionWavePolicy.WaveCount(50,10,true,true)==4,"larger quotas preserve the exact fifty-kill goal");
        for(int supply=0;supply<=500;supply+=13)
            foreach(int authored in new[]{int.MinValue,1,10,50,int.MaxValue})
            {
                int emitted=0,waves=IntercomMissionWavePolicy.WaveCount(supply,authored,true,true);
                for(int wave=1;wave<=waves;wave++)emitted+=Math.Min(supply-emitted,IntercomMissionWavePolicy.WaveSize(authored,wave,true,true));
                Check(emitted==supply&&waves<=supply,"continuous custom quotas terminate exactly within bounded supply");
            }
        Check(IntercomMissionWavePolicy.WaveSize(10,int.MinValue,true,true)==10
            &&IntercomMissionWavePolicy.WaveSize(10,int.MaxValue,true,true)==26,"continuous stage clamps before arithmetic can overflow");
        for(int ordinal=0;ordinal<10;ordinal++)Check(IntercomMissionWavePolicy.ArchetypeIndex(1,ordinal,true,true)!=2,"first continuous quota introduces runners before heavies");
        Check(IntercomMissionWavePolicy.ArchetypeIndex(1,3,true,true)==1
            &&IntercomMissionWavePolicy.ArchetypeIndex(2,5,true,true)==2,"new composition introduces threats in a predictable order");
    }

    private static void RunSummaries()
    {
        var won=new IntercomMissionRunSummary("0123456789abcdef0123456789abcdef",3,2,IntercomMissionMode.Elimination,
            IntercomMissionPhase.Victory,50,1,5,187.25f);
        Check(won.Won&&won.Kills==50&&won.Rescued==0&&won.WaveReached==5&&won.ElapsedSeconds==187.25f,
            "elimination result preserves exact kills, time and wave without manufacturing a rescue");
        var rescued=new IntercomMissionRunSummary("rescue",4,3,IntercomMissionMode.Rescue,IntercomMissionPhase.Victory,12,1,3,77);
        Check(rescued.Won&&rescued.Rescued==1&&rescued.Kills==12,"successful escort records one saved ally independently of kills");
        var lost=new IntercomMissionRunSummary(null,5,4,IntercomMissionMode.Rescue,IntercomMissionPhase.Defeat,9,1,2,float.NaN);
        Check(!lost.Won&&lost.Rescued==0&&lost.RunId==""&&lost.ElapsedSeconds==0,"failed escort cannot award a saved ally and invalid time is normalized");
        var free=new IntercomMissionRunSummary("free",6,5,IntercomMissionMode.FreePlay,IntercomMissionPhase.Defeat,500,1,500,9);
        Check(free.Kills==0&&free.Rescued==0&&free.WaveReached==0,"free play cannot fabricate combat or rescue rewards");
        foreach(float invalid in new[]{float.PositiveInfinity,float.NegativeInfinity,-1f})
            Check(new IntercomMissionRunSummary("x",1,1,IntercomMissionMode.Elimination,IntercomMissionPhase.Defeat,-1,0,-1,invalid).ElapsedSeconds==0,
                "non-finite or negative result duration is safe");
        var state=new IntercomMissionState();state.Begin(IntercomMissionMode.Rescue,1,1);state.Select();
        Check(won.Kills==50&&won.RunRevision==3&&won.ResultRevision==2,"completed summary is immutable across accounting resets");
    }

    private static void SelectionAndLimits()
    {
        var state = new IntercomMissionState();
        Check(state.Phase == IntercomMissionPhase.Selection, "fresh session opens selection");
        Check(!state.RecordSpawn(0, 1, state.Epoch), "selection cannot spawn");
        Check(!state.RecordKill(0, 1, state.Epoch), "selection cannot award kills");
        Check(!state.CompleteRescue(true,true,true) && !state.Fail(), "selection has no outcome");
        state.Begin((IntercomMissionMode)42, 0, int.MaxValue);
        Check(state.Mode == IntercomMissionMode.Elimination && state.Goal == 1 && state.Supply == 1, "invalid mode and elimination goal sanitized");
        Check(state.Phase == IntercomMissionPhase.Active, "begin enables mission");
        state.Begin(IntercomMissionMode.Rescue, int.MaxValue, -9);
        Check(state.Goal == 500 && state.Supply == 1, "rescue bounds sanitized independently");
        state.Begin(IntercomMissionMode.Rescue, 0, int.MaxValue);
        Check(state.Goal == 1 && state.Supply == 500, "upper supply bounded");
        uint epoch = state.Epoch;
        state.Select();
        Check(state.Epoch != epoch && state.Phase == IntercomMissionPhase.Selection, "selection invalidates previous run");
        Check(state.Kills == 0 && state.Spawned == 0 && state.Alive == 0, "selection clears counters");
    }

    private static void ExactElimination()
    {
        var state = new IntercomMissionState(); state.Begin(IntercomMissionMode.Elimination, 50);
        uint epoch = state.Epoch;
        for(int i = 0; i < 50; i++)
        {
            int slot = i % 8; uint life = (uint)(i/8 + 1);
            Check(state.RecordSpawn(slot,life,epoch), "new pooled life counted");
            Check(state.Alive == 1 && state.Spawned == i+1, "spawn totals maintained");
            Check(state.RecordKill(slot,life,epoch), "registered death counted");
            Check(!state.RecordKill(slot,life,epoch), "duplicate death not counted");
            Check(state.Kills == i+1 && state.Alive == 0, "one kill per life");
            Check(state.Phase == (i < 49 ? IntercomMissionPhase.Active : IntercomMissionPhase.Victory), "victory exactly at fifty");
        }
        Check(!state.RecordSpawn(0,100,epoch), "no fifty-first actor after victory");
        Check(!state.Fail() && state.Phase == IntercomMissionPhase.Victory, "late damage cannot overwrite victory");
        Check(state.Kills == 50 && state.Spawned == 50, "goal counters do not overflow");
        state.Begin(IntercomMissionMode.Elimination, 3, 500);
        for(int i=0;i<3;i++) Check(state.RecordSpawn(i,1,state.Epoch), "elimination authored goal bounds supply");
        Check(!state.RecordSpawn(4,1,state.Epoch), "supply cannot exceed kill objective");
        Check(state.Alive==3 && state.Phase==IntercomMissionPhase.Active, "spawning all enemies is not victory");
    }

    private static void PoolIdentity()
    {
        var state = new IntercomMissionState(); state.Begin(IntercomMissionMode.Elimination,50);
        uint epoch = state.Epoch;
        foreach(int slot in new[]{-1,16,int.MinValue,int.MaxValue})
        {
            Check(!state.RecordSpawn(slot,1,epoch), "invalid slot spawn rejected");
            Check(!state.RecordKill(slot,1,epoch), "invalid slot death rejected");
        }
        Check(!state.RecordSpawn(0,0,epoch), "zero life invalid");
        Check(!state.RecordSpawn(0,1,epoch+1), "future mission spawn rejected");
        Check(!state.RecordKill(0,1,epoch), "unregistered enemy cannot award kill");
        Check(state.RecordSpawn(0,1,epoch), "initial slot acquired");
        Check(!state.RecordSpawn(0,1,epoch) && !state.RecordSpawn(0,2,epoch), "live slot cannot be duplicated or replaced");
        Check(!state.RecordKill(0,2,epoch), "wrong life cannot kill current actor");
        Check(state.RecordKill(0,1,epoch), "first actor expires");
        Check(!state.RecordSpawn(0,1,epoch), "same life cannot respawn");
        Check(state.RecordSpawn(0,2,epoch), "new pooled life can respawn");
        Check(!state.RecordKill(0,1,epoch), "old death cannot kill reused actor");
        Check(state.Kills==1 && state.Alive==1 && state.Spawned==2, "rejected stale callback leaves counters intact");
        state.Begin(IntercomMissionMode.Elimination,50);
        Check(state.Epoch!=epoch && state.Kills==0 && state.Spawned==0 && state.Alive==0, "new run isolated");
        Check(state.RecordSpawn(0,2,state.Epoch), "same actor life number may exist in a new run");
        Check(!state.RecordKill(0,2,epoch), "previous run same slot and life rejected");
        Check(!state.RecordSpawn(1,1,epoch), "previous run spawn rejected");
        Check(state.RecordKill(0,2,state.Epoch), "current run death accepted");
    }

    private static void TerminalOutcomes()
    {
        var state = new IntercomMissionState(); state.Begin(IntercomMissionMode.Elimination,50);
        Check(state.RecordSpawn(0,1,state.Epoch), "failure fixture spawned");
        Check(state.Fail(), "first defeat accepted");
        Check(!state.Fail(), "defeat is terminal and idempotent");
        Check(!state.RecordKill(0,1,state.Epoch) && !state.RecordSpawn(1,1,state.Epoch), "post-defeat enemy events ignored");
        Check(state.Kills==0 && state.Phase==IntercomMissionPhase.Defeat, "post-defeat events cannot change result");
        uint epoch = state.Epoch;
        state.Select(); state.Begin(IntercomMissionMode.Rescue,50,3);
        Check(state.Epoch!=epoch && state.Phase==IntercomMissionPhase.Active && state.Alive==0, "mode change starts clean");
        Check(!state.RecordKill(0,1,epoch), "prior mode death ignored");
    }

    private static void Rescue()
    {
        for(int mask=0;mask<8;mask++)
        {
            var state = new IntercomMissionState(); state.Begin(IntercomMissionMode.Rescue,50,2);
            bool result = state.CompleteRescue((mask&1)!=0,(mask&2)!=0,(mask&4)!=0);
            Check(result==(mask==7), "rescue needs recruited ally and both inside zone");
            Check(state.Phase==(mask==7 ? IntercomMissionPhase.Victory : IntercomMissionPhase.Active), "incomplete rescue stays active");
            if(mask==7)
            {
                Check(!state.CompleteRescue(true,true,true) && !state.Fail(), "rescued outcome cannot be repeated or lost");
                Check(state.Kills==0, "rescue victory does not fabricate kills");
            }
        }
        var rescue = new IntercomMissionState(); rescue.Begin(IntercomMissionMode.Rescue,1,2);
        for(int i=0;i<2;i++)
        { Check(rescue.RecordSpawn(i,1,rescue.Epoch), "rescue reinforcement spawned"); Check(rescue.RecordKill(i,1,rescue.Epoch), "rescue kill tracked"); }
        Check(!rescue.RecordSpawn(2,1,rescue.Epoch), "rescue finite supply enforced");
        Check(rescue.Kills==2 && rescue.Alive==0 && rescue.Phase==IntercomMissionPhase.Active, "defeating all attackers does not bypass rescue");
        Check(rescue.CompleteRescue(true,true,true), "escort can complete when reinforcement supply exhausted");
        var elimination = new IntercomMissionState(); elimination.Begin(IntercomMissionMode.Elimination,50);
        Check(!elimination.CompleteRescue(true,true,true), "rescue event cannot win elimination");
        var failed = new IntercomMissionState(); failed.Begin(IntercomMissionMode.Rescue,50);failed.Fail();
        Check(!failed.CompleteRescue(true,true,true), "dead ally cannot be rescued after failure");
    }

    private static void RandomizedAccounting()
    {
        var rng=new Random(7903);
        for(int run=0;run<40;run++)
        {
            var state=new IntercomMissionState();state.Begin(IntercomMissionMode.Rescue,50,200);
            uint epoch=state.Epoch;
            uint[] life=new uint[16];bool[] alive=new bool[16];int spawned=0,kills=0,living=0;
            for(int step=0;step<350;step++)
            {
                int slot=rng.Next(16);bool spawn=rng.Next(2)==0;
                uint proposed=spawn ? life[slot]+1 : life[slot];
                uint proposedEpoch=rng.Next(7)==0 ? epoch-1 : epoch;
                if(rng.Next(9)==0) proposed++;
                bool expected;
                if(spawn)
                {
                    expected=proposedEpoch==epoch&&!alive[slot]&&proposed!=0&&proposed!=life[slot]&&spawned<200;
                    Check(state.RecordSpawn(slot,proposed,proposedEpoch)==expected,"random spawn matches independent identity model");
                    if(expected){life[slot]=proposed;alive[slot]=true;spawned++;living++;}
                }
                else
                {
                    expected=proposedEpoch==epoch&&alive[slot]&&proposed==life[slot];
                    Check(state.RecordKill(slot,proposed,proposedEpoch)==expected,"random death matches independent identity model");
                    if(expected){alive[slot]=false;kills++;living--;}
                }
                Check(state.Spawned==spawned&&state.Kills==kills&&state.Alive==living,"random counters remain conserved");
                Check(state.Spawned==state.Kills+state.Alive,"all spawned actors are alive or killed exactly once");
                Check(state.Phase==IntercomMissionPhase.Active,"random rescue combat never awards rescue victory");
            }
        }
    }
}
