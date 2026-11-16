using System;

namespace IntercomMvp
{
    /// <summary>A bounded per-run ledger. Only consuming a live token counts as collected currency.</summary>
    public sealed class IntercomGearCollectionState
    {
        public const int Capacity = 96, MaximumRunGears = 10000;
        private readonly int[] values = new int[Capacity];
        public int CollectedGears { get; private set; }
        public int ActiveCount { get; private set; }
        public int ActiveValue { get; private set; }
        public int ValueAt(int slot) => slot >= 0 && slot < Capacity ? values[slot] : 0;

        /// <summary>A full pool merges value into a visible coin instead of creating objects or losing loot.</summary>
        public bool TryDrop(int value, out int slot, out bool activated)
        {
            slot = -1; activated = false;
            if (value <= 0 || CollectedGears + ActiveValue >= MaximumRunGears) return false;
            value = Math.Min(value, MaximumRunGears - CollectedGears - ActiveValue);
            for (int i = 0; i < Capacity; i++)
                if (values[i] == 0) { slot = i; activated = true; break; }
            if (slot < 0) slot = 0;
            values[slot] += value; ActiveValue += value;
            if (activated) ActiveCount++;
            return true;
        }
        public bool TryCollect(int slot, out int value)
        {
            value = ValueAt(slot);
            if (value <= 0) return false;
            values[slot] = 0; ActiveCount--; ActiveValue -= value; CollectedGears += value;
            return true;
        }
        public void ClearUncollected() { Array.Clear(values, 0, values.Length); ActiveValue = ActiveCount = 0; }
        public void BeginRun() { ClearUncollected(); CollectedGears = 0; }
    }
}
