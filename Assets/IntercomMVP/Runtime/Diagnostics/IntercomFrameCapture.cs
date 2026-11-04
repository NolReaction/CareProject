using System;

namespace IntercomMvp
{
    /// <summary>Bounded chronological frame history. No strings or allocations while recording.</summary>
    public sealed class IntercomFrameCapture
    {
        public struct Sample
        {
            public int frame, alive, spawned, kills, prepared, navNodes, shells, effects, gcCollections;
            public int occlusionRebuilds, geometryBuilds;
            public double wallMilliseconds;
            // -1 means this platform/build did not expose the counter.
            public long mainThreadNanoseconds, gcAllocatedBytes;
        }
        private readonly Sample[] samples;
        private int next;
        public int Count { get; private set; }
        public int Capacity => samples.Length;
        public IntercomFrameCapture(int capacity = 3600)
        { samples = new Sample[Math.Max(1, Math.Min(36000, capacity))]; }
        public void Clear() { next = Count = 0; }
        public void Add(Sample value)
        {
            if (double.IsNaN(value.wallMilliseconds) || double.IsInfinity(value.wallMilliseconds)
                || value.wallMilliseconds < 0) return;
            samples[next] = value; next = (next + 1) % samples.Length;
            if (Count < samples.Length) Count++;
        }
        public Sample GetOldest(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return samples[(next - Count + index + samples.Length) % samples.Length];
        }
    }
}
