using System;

namespace IntercomMvp
{
    public enum IntercomMissionMode { Elimination, Rescue, FreePlay }
    public enum IntercomMissionPhase { Selection, Active, Victory, Defeat }

    /// <summary>A completed run is a value snapshot; menu navigation cannot change its earned result.</summary>
    public readonly struct IntercomMissionRunSummary
    {
        public readonly string RunId;
        public readonly uint RunRevision, ResultRevision;
        public readonly IntercomMissionMode Mode;
        public readonly IntercomMissionPhase Outcome;
        public readonly int Kills, Rescued, WaveReached;
        public readonly float ElapsedSeconds;
        public bool Won => Outcome == IntercomMissionPhase.Victory;

        public IntercomMissionRunSummary(string runId, uint runRevision, uint resultRevision,
            IntercomMissionMode mode, IntercomMissionPhase outcome, int kills, int rescued,
            int waveReached, float elapsedSeconds)
        {
            RunId = runId ?? ""; RunRevision = runRevision; ResultRevision = resultRevision;
            Mode = mode == IntercomMissionMode.Rescue || mode == IntercomMissionMode.FreePlay
                ? mode : IntercomMissionMode.Elimination;
            Outcome = outcome == IntercomMissionPhase.Victory ? outcome : IntercomMissionPhase.Defeat;
            Kills = Mode == IntercomMissionMode.FreePlay ? 0 : Math.Max(0, Math.Min(500, kills));
            Rescued = Mode == IntercomMissionMode.Rescue && Outcome == IntercomMissionPhase.Victory && rescued > 0 ? 1 : 0;
            WaveReached = Mode == IntercomMissionMode.FreePlay ? 0 : Math.Max(1, Math.Min(500, waveReached));
            ElapsedSeconds = float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds)
                ? 0f : Math.Max(0f, elapsedSeconds);
        }
    }

    /// <summary>Deterministic quotas/composition. Counts are measured within each wave, never by pool slot.</summary>
    public static class IntercomMissionWavePolicy
    {
        public static int WaveSize(int authoredSize, int wave, bool rhythmic)
        {
            int size = Math.Max(1, Math.Min(50, authoredSize));
            if (!rhythmic) return size;
            // A ten-enemy baseline becomes 6, 8, 10, 12, 14. Custom small waves still make progress.
            int stage = wave <= 1 ? 0 : wave >= 5 ? 4 : wave - 1;
            return Math.Max(1, Math.Min(50, size * (3 + stage) / 5));
        }
        public static int WaveCount(int supply, int authoredSize, bool rhythmic)
        {
            int remaining = Math.Max(0, Math.Min(500, supply)), count = 0;
            while (remaining > 0) { count++; remaining -= WaveSize(authoredSize, count, rhythmic); }
            return count;
        }
        public static int AliveLimit(int authoredLimit, int wave, bool rhythmic)
        {
            int limit = Math.Max(1, Math.Min(8, authoredLimit));
            return rhythmic ? Math.Min(limit, 2 + Math.Max(1, Math.Min(6, wave))) : limit;
        }
        public static float PaceMultiplier(int wave, bool rhythmic)
        {
            if (!rhythmic) return 1f;
            return wave <= 1 ? 1.4f : wave == 2 ? 1.15f : wave == 3 ? 1f : wave == 4 ? .9f : .8f;
        }
        /// <returns>0 standard, 1 runner, 2 heavy. The runtime enum keeps these stable serialized values.</returns>
        public static int ArchetypeIndex(int wave, int ordinal, bool rhythmic)
        {
            if (!rhythmic || wave <= 1) return 0;
            ordinal = Math.Max(0, ordinal);
            if (wave >= 3 && ordinal % 5 == 4) return 2;
            return ordinal % 4 == 2 ? 1 : 0;
        }
    }

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
