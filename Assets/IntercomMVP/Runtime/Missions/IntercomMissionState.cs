using System;

namespace IntercomMvp
{
    public enum IntercomMissionMode { Elimination, Rescue, FreePlay }
    public enum IntercomMissionPhase { Selection, Active, Victory, Defeat }

    /// <summary>Mission accounting is keyed by run, pool slot and actor life. Presentation cannot award kills.</summary>
    public sealed class IntercomMissionState
    {
        public IntercomMissionMode Mode { get; private set; }
        public IntercomMissionPhase Phase { get; private set; } = IntercomMissionPhase.Selection;
        public uint Epoch { get; private set; }
        public int Kills { get; private set; }
        public int Goal { get; private set; } = 50;
        public int Spawned { get; private set; }
        public int Alive { get; private set; }
        public int Supply { get; private set; }
        private readonly uint[] lives = new uint[16];
        private readonly bool[] alive = new bool[16];

        public void Begin(IntercomMissionMode mode, int goal, int supply = 50)
        {
            Mode = mode == IntercomMissionMode.Rescue || mode == IntercomMissionMode.FreePlay ? mode : IntercomMissionMode.Elimination;
            Goal = Mode == IntercomMissionMode.FreePlay ? 0 : Math.Max(1, Math.Min(500, goal));
            Supply = Mode == IntercomMissionMode.FreePlay ? 0
                : Mode == IntercomMissionMode.Elimination ? Goal : Math.Max(1, Math.Min(500, supply));
            Clear(); Phase = IntercomMissionPhase.Active;
        }
        public void Select() { Clear(); Phase = IntercomMissionPhase.Selection; }
        private void Clear()
        {
            unchecked { Epoch++; }
            Kills = Spawned = Alive = 0;
            Array.Clear(lives, 0, lives.Length); Array.Clear(alive, 0, alive.Length);
        }
        public bool RecordSpawn(int slot, uint life, uint epoch)
        {
            if(Phase != IntercomMissionPhase.Active || epoch != Epoch || slot < 0 || slot >= lives.Length
                || life == 0 || alive[slot] || lives[slot] == life || Spawned >= Supply) return false;
            lives[slot] = life; alive[slot] = true; Spawned++; Alive++; return true;
        }
        public bool RecordKill(int slot, uint life, uint epoch)
        {
            if(Phase != IntercomMissionPhase.Active || epoch != Epoch || slot < 0 || slot >= lives.Length
                || !alive[slot] || lives[slot] != life) return false;
            alive[slot] = false; Alive--; Kills++;
            if(Mode == IntercomMissionMode.Elimination && Kills >= Goal) Phase = IntercomMissionPhase.Victory;
            return true;
        }
        public bool CompleteRescue(bool recruited, bool allyInZone, bool heroInZone)
        {
            if(Phase != IntercomMissionPhase.Active || Mode != IntercomMissionMode.Rescue
                || !recruited || !allyInZone || !heroInZone) return false;
            Phase = IntercomMissionPhase.Victory; return true;
        }
        public bool Fail()
        {
            if(Phase != IntercomMissionPhase.Active) return false;
            Phase = IntercomMissionPhase.Defeat; return true;
        }
    }
}
