using System;
using IntercomMvp;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label)
    { checks++; if(!value) throw new Exception(label); }
    private static void Main()
    {
        SelectionAndLimits(); ExactElimination(); PoolIdentity(); TerminalOutcomes(); Rescue(); RandomizedAccounting();
        Console.WriteLine($"Mission state: {checks} assertions passed. Production mission accounting; no Unity runtime or rendering.");
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
