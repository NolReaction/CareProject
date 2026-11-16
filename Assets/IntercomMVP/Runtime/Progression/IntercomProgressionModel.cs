using System;
using System.Globalization;

namespace IntercomMvp
{
    /// <summary>Local MVP rewards and one finite upgrade branch. Values are save data, never shared asset mutations.</summary>
    public readonly struct IntercomRunReward
    {
        public readonly int Kills, Rescued, KillParts, RescueParts, VictoryParts;
        public readonly bool Won, Eligible;
        public int Total => KillParts + RescueParts + VictoryParts;
        public IntercomRunReward(int kills, int rescued, bool won, bool eligible, int room)
        {
            Kills = Math.Max(0, Math.Min(10000, kills));
            Rescued = won ? Math.Max(0, Math.Min(1, rescued)) : 0; Won = won; Eligible = eligible;
            room = Math.Max(0, Math.Min(IntercomProgressionState.MaximumWallet, room));
            KillParts = eligible ? Math.Min(Kills, room) : 0; room -= KillParts;
            RescueParts = eligible ? Math.Min(Rescued * 25, room) : 0; room -= RescueParts;
            VictoryParts = eligible && won ? Math.Min(20, room) : 0;
        }
    }

    public sealed class IntercomProgressionState
    {
        public const int MaximumMagazineLevel = 3, MagazineRoundsPerLevel = 6, MaximumWallet = 999999;
        public const int MaximumStatistic = 999999999;
        public readonly int Generation, Wallet, MagazineLevel, CompletedRuns, TotalKills, TotalRescued, LastCredit;
        public readonly string LastRunId;
        public int MagazineBonus => MagazineLevel * MagazineRoundsPerLevel;
        public int NextMagazineCost => Cost(MagazineLevel);
        public bool CanBuyMagazine => Generation < int.MaxValue && NextMagazineCost > 0 && Wallet >= NextMagazineCost;

        public IntercomProgressionState() : this(0, 0, 0, 0, 0, 0, "-", 0) { }
        private IntercomProgressionState(int generation, int wallet, int level, int runs, int kills,
            int rescued, string runId, int credit)
        {
            Generation = generation; Wallet = wallet; MagazineLevel = level; CompletedRuns = runs;
            TotalKills = kills; TotalRescued = rescued; LastRunId = runId; LastCredit = credit;
        }
        public static int Cost(int level) => level == 0 ? 60 : level == 1 ? 130 : level == 2 ? 240 : 0;
        public static bool ValidRunId(string value)
        {
            if (value == null || value.Length != 32) return false;
            for (int i = 0; i < value.Length; i++)
                if (!(value[i] >= '0' && value[i] <= '9') && !(value[i] >= 'a' && value[i] <= 'f')) return false;
            return true;
        }
        public bool TrySettle(string runId, int kills, int rescued, bool won, bool eligible,
            out IntercomProgressionState next, out IntercomRunReward reward)
        {
            next = this;
            bool valid = eligible && ValidRunId(runId);
            reward = new IntercomRunReward(kills, rescued, won, valid,
                runId == LastRunId ? LastCredit : MaximumWallet - Wallet);
            if (!valid || runId == LastRunId || Generation == int.MaxValue) return false;
            next = new IntercomProgressionState(Generation + 1, Wallet + reward.Total, MagazineLevel,
                Add(CompletedRuns, 1), Add(TotalKills, reward.Kills), Add(TotalRescued, reward.Rescued), runId, reward.Total);
            return true;
        }
        public bool TryBuyMagazine(out IntercomProgressionState next)
        {
            next = this;
            if (!CanBuyMagazine || Generation == int.MaxValue) return false;
            next = new IntercomProgressionState(Generation + 1, Wallet - NextMagazineCost, MagazineLevel + 1,
                CompletedRuns, TotalKills, TotalRescued, LastRunId, LastCredit);
            return true;
        }
        private static int Add(int value, int amount) => (int)Math.Min(MaximumStatistic, (long)value + amount);

        public string Encode()
        {
            string payload = string.Join("|", "CP1", N(Generation), N(Wallet), N(MagazineLevel), N(CompletedRuns),
                N(TotalKills), N(TotalRescued), LastRunId, N(LastCredit));
            return payload + "|" + Checksum(payload);
        }
        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        // Integrity checksum detects truncated/edited preference bytes; it is deliberately not an anti-cheat claim.
        private static string Checksum(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < text.Length; i++) { hash ^= text[i]; hash *= 1099511628211UL; }
                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }
        public static bool TryDecode(string encoded, out IntercomProgressionState state)
        {
            state = null;
            if (string.IsNullOrEmpty(encoded) || encoded.Length > 256) return false;
            string[] p = encoded.Split('|');
            if (p.Length != 10 || p[0] != "CP1") return false;
            int cut = encoded.LastIndexOf('|');
            if (!string.Equals(Checksum(encoded.Substring(0, cut)), p[9], StringComparison.Ordinal)) return false;
            int[] n = new int[7]; int[] indexes = { 1, 2, 3, 4, 5, 6, 8 };
            for (int i = 0; i < n.Length; i++)
                if (!int.TryParse(p[indexes[i]], NumberStyles.None, CultureInfo.InvariantCulture, out n[i]) || n[i] < 0) return false;
            if (n[1] > MaximumWallet || n[2] > MaximumMagazineLevel || n[3] > MaximumStatistic
                || n[4] > MaximumStatistic || n[5] > MaximumStatistic || n[6] > 10045) return false;
            if (p[7] != "-" && !ValidRunId(p[7])) return false;
            if (p[7] == "-" && n[6] != 0) return false;
            state = new IntercomProgressionState(n[0], n[1], n[2], n[3], n[4], n[5], p[7], n[6]);
            return true;
        }
        public static IntercomProgressionState Recover(string a, string b, out bool recovered)
        {
            bool av = TryDecode(a, out IntercomProgressionState first), bv = TryDecode(b, out IntercomProgressionState second);
            recovered = (!string.IsNullOrEmpty(a) && !av) || (!string.IsNullOrEmpty(b) && !bv);
            if (av && bv) return first.Generation >= second.Generation ? first : second;
            return av ? first : bv ? second : new IntercomProgressionState();
        }
    }
}
