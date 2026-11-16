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

    /// <summary>Bounded native-soak statistics. Recording allocates nothing; percentiles are computed only at export.</summary>
    public sealed class IntercomSoakFrameMetrics
    {
        [Serializable]
        public struct Summary
        {
            public int frames, retainedFrames, framesOver33ms, framesOver50ms, mainThreadSamples, gcSamples, peakAlive;
            public double meanWallMilliseconds, p95WallMilliseconds, p99WallMilliseconds, maximumWallMilliseconds;
            public long maximumMainThreadNanoseconds, maximumGcAllocatedBytes, totalGcAllocatedBytes;
        }
        private readonly double[] wall;
        private int next, retained;
        private Summary total;
        private double sumWall;
        public IntercomSoakFrameMetrics(int capacity=36000)
        {
            wall=new double[Math.Max(1,Math.Min(36000,capacity))];
            total.maximumMainThreadNanoseconds=total.maximumGcAllocatedBytes=-1;
        }
        public bool Add(IntercomFrameCapture.Sample sample)
        {
            double ms=sample.wallMilliseconds;
            if(double.IsNaN(ms)||double.IsInfinity(ms)||ms<0) return false;
            wall[next]=ms; next=(next+1)%wall.Length; retained=Math.Min(retained+1,wall.Length);
            total.frames++;sumWall=ms>double.MaxValue-sumWall?double.MaxValue:sumWall+ms;
            total.maximumWallMilliseconds=Math.Max(total.maximumWallMilliseconds,ms);
            if(ms>33.333333)total.framesOver33ms++;
            if(ms>50)total.framesOver50ms++;
            total.peakAlive=Math.Max(total.peakAlive,sample.alive);
            if(sample.mainThreadNanoseconds>=0)
            { total.mainThreadSamples++;total.maximumMainThreadNanoseconds=Math.Max(total.maximumMainThreadNanoseconds,sample.mainThreadNanoseconds); }
            if(sample.gcAllocatedBytes>=0)
            {
                total.gcSamples++;total.maximumGcAllocatedBytes=Math.Max(total.maximumGcAllocatedBytes,sample.gcAllocatedBytes);
                total.totalGcAllocatedBytes=sample.gcAllocatedBytes>long.MaxValue-total.totalGcAllocatedBytes
                    ?long.MaxValue:total.totalGcAllocatedBytes+sample.gcAllocatedBytes;
            }
            return true;
        }
        public Summary Snapshot()
        {
            Summary result=total; result.retainedFrames=retained;
            result.meanWallMilliseconds=total.frames>0?sumWall/total.frames:0;
            if(retained==0)return result;
            var ordered=new double[retained];Array.Copy(wall,ordered,retained);Array.Sort(ordered);
            result.p95WallMilliseconds=ordered[Math.Max(0,(int)Math.Ceiling(retained*.95)-1)];
            result.p99WallMilliseconds=ordered[Math.Max(0,(int)Math.Ceiling(retained*.99)-1)];
            return result;
        }
    }
}
