using System;
using System.Collections.Generic;

namespace IntercomMvp
{
    public readonly struct IntercomNavPoint
    {
        public readonly float X, Z;
        public IntercomNavPoint(float x, float z) { X = x; Z = z; }
    }

    /// <summary>Bounded reusable A* workspace for a flat floor. No Unity objects or per-search arrays.</summary>
    public sealed class IntercomNavGrid
    {
        public readonly float MinX, MinZ, CellSize;
        public readonly int Width, Depth;
        public int Count => Width * Depth;
        public uint Version { get; private set; }
        public int LastExpandedNodes { get; private set; }
        private readonly bool[] blocked, pending;
        private readonly int[] cost, parent, stamp, closed, heap, heapPosition, reversePath;
        private int searchStamp, heapCount, goalCell;

        public IntercomNavGrid(float minX, float minZ, float width, float depth, float cellSize)
        {
            if (!Finite(minX) || !Finite(minZ) || !Finite(width) || !Finite(depth)
                || !Finite(cellSize) || width <= 0f || depth <= 0f || cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width));
            MinX = minX; MinZ = minZ; CellSize = cellSize;
            Width = (int)Math.Ceiling(width / cellSize); Depth = (int)Math.Ceiling(depth / cellSize);
            if (Width < 1 || Depth < 1 || (long)Width * Depth > 32768)
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Navigation is limited to 32768 cells.");
            blocked = new bool[Count]; pending = new bool[Count];
            cost = new int[Count]; parent = new int[Count]; stamp = new int[Count];
            closed = new int[Count]; heap = new int[Count]; heapPosition = new int[Count];
            reversePath = new int[Count];
        }

        public void BeginObstacles() => Array.Clear(pending, 0, pending.Length);

        public void AddObstacle(float minX, float minZ, float maxX, float maxZ, float inflation)
        {
            if (!Finite(minX) || !Finite(minZ) || !Finite(maxX) || !Finite(maxZ)
                || !Finite(inflation) || minX > maxX || minZ > maxZ) return;
            inflation = Math.Max(0f, inflation);
            minX -= inflation; minZ -= inflation; maxX += inflation; maxZ += inflation;
            if (maxX < MinX || maxZ < MinZ || minX >= MinX + Width * CellSize || minZ >= MinZ + Depth * CellSize) return;
            minX = Math.Max(MinX, minX); minZ = Math.Max(MinZ, minZ);
            maxX = Math.Min(MinX + Width * CellSize, maxX); maxZ = Math.Min(MinZ + Depth * CellSize, maxZ);
            // Any cell intersecting the inflated obstacle is blocked. This keeps
            // diagonal and smoothed paths conservative between cell centres.
            int x0 = Math.Max(0, (int)Math.Floor((minX - MinX) / CellSize));
            int z0 = Math.Max(0, (int)Math.Floor((minZ - MinZ) / CellSize));
            int x1 = Math.Min(Width - 1, (int)Math.Floor((maxX - MinX) / CellSize));
            int z1 = Math.Min(Depth - 1, (int)Math.Floor((maxZ - MinZ) / CellSize));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++) pending[z * Width + x] = true;
        }

        public bool EndObstacles()
        {
            bool changed = false;
            for (int i = 0; i < Count; i++)
                if (blocked[i] != pending[i]) { blocked[i] = pending[i]; changed = true; }
            if (changed) unchecked { Version++; }
            return changed;
        }

        public bool IsWalkable(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth && !blocked[z * Width + x];
        public IntercomNavPoint Center(int x, int z) => new IntercomNavPoint(MinX + (x + .5f) * CellSize, MinZ + (z + .5f) * CellSize);

        public bool TryCell(float x, float z, out int cellX, out int cellZ)
        {
            cellX = cellZ = -1;
            if (!Finite(x) || !Finite(z) || x < MinX || z < MinZ
                || x >= MinX + Width * CellSize || z >= MinZ + Depth * CellSize) return false;
            cellX = (int)Math.Floor((x - MinX) / CellSize);
            cellZ = (int)Math.Floor((z - MinZ) / CellSize);
            return true;
        }

        public bool TryProject(float x, float z, float maximumSnap, out IntercomNavPoint point)
        {
            point = default;
            if (!TryCell(x, z, out int cx, out int cz) || !Finite(maximumSnap) || maximumSnap < 0f) return false;
            if (IsWalkable(cx, cz)) { point = Center(cx, cz); return true; }
            int range = Math.Min(Math.Max(Width, Depth), (int)Math.Ceiling(maximumSnap / CellSize) + 1);
            float best = maximumSnap * maximumSnap;
            bool found = false;
            for (int nz = Math.Max(0, cz - range); nz <= Math.Min(Depth - 1, cz + range); nz++)
                for (int nx = Math.Max(0, cx - range); nx <= Math.Min(Width - 1, cx + range); nx++)
                {
                    if (!IsWalkable(nx, nz)) continue;
                    IntercomNavPoint candidate = Center(nx, nz);
                    float dx = candidate.X - x, dz = candidate.Z - z, distance = dx * dx + dz * dz;
                    if (distance > best || (found && distance == best)) continue;
                    best = distance; point = candidate; found = true;
                }
            return found;
        }

        /// <summary>Exact supercover traversal. Touching a blocked corner is not a shortcut.</summary>
        public bool SegmentClear(float ax, float az, float bx, float bz)
        {
            if (!TryCell(ax, az, out int x, out int z) || !TryCell(bx, bz, out int endX, out int endZ)) return false;
            double dx = bx - ax, dz = bz - az;
            int sx = Math.Sign(dx), sz = Math.Sign(dz);
            bool verticalEdge = sx == 0 && OnEdge((ax - MinX) / CellSize);
            bool horizontalEdge = sz == 0 && OnEdge((az - MinZ) / CellSize);
            double deltaX = sx == 0 ? double.PositiveInfinity : CellSize / Math.Abs(dx);
            double deltaZ = sz == 0 ? double.PositiveInfinity : CellSize / Math.Abs(dz);
            double nextX = sx == 0 ? double.PositiveInfinity : (MinX + (x + (sx > 0 ? 1 : 0)) * CellSize - ax) / dx;
            double nextZ = sz == 0 ? double.PositiveInfinity : (MinZ + (z + (sz > 0 ? 1 : 0)) * CellSize - az) / dz;
            for (int n = 0; n <= Width + Depth + 2; n++)
            {
                if (!IsWalkable(x, z) || (verticalEdge && !IsWalkable(x - 1, z))
                    || (horizontalEdge && !IsWalkable(x, z - 1))) return false;
                if (x == endX && z == endZ) return true;
                if (Math.Abs(nextX - nextZ) < .0000001d)
                {
                    if (!IsWalkable(x + sx, z) || !IsWalkable(x, z + sz)) return false;
                    x += sx; z += sz; nextX += deltaX; nextZ += deltaZ;
                }
                else if (nextX < nextZ) { x += sx; nextX += deltaX; }
                else { z += sz; nextZ += deltaZ; }
            }
            return false;
        }

        public bool FindPath(float startX, float startZ, float goalX, float goalZ, float goalSnap, List<IntercomNavPoint> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear(); LastExpandedNodes = 0;
            if (!TryCell(startX, startZ, out int sx, out int sz) || !IsWalkable(sx, sz)
                || !TryProject(goalX, goalZ, goalSnap, out IntercomNavPoint projected)
                || !TryCell(projected.X, projected.Z, out int gx, out int gz)) return false;
            int start = sz * Width + sx; goalCell = gz * Width + gx;
            if (SegmentClear(startX, startZ, projected.X, projected.Z))
            { output.Add(projected); return true; }
            if (++searchStamp == int.MaxValue)
            { Array.Clear(stamp, 0, Count); Array.Clear(closed, 0, Count); searchStamp = 1; }
            heapCount = 0; Initialize(start); cost[start] = 0; Push(start);
            while (heapCount > 0 && LastExpandedNodes < Count)
            {
                int cell = Pop(); closed[cell] = searchStamp; LastExpandedNodes++;
                if (cell == goalCell) return BuildPath(start, startX, startZ, output);
                int x = cell % Width, z = cell / Width;
                for (int oz = -1; oz <= 1; oz++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if ((ox == 0 && oz == 0) || !IsWalkable(x + ox, z + oz)) continue;
                        if (ox != 0 && oz != 0 && (!IsWalkable(x + ox, z) || !IsWalkable(x, z + oz))) continue;
                        int next = (z + oz) * Width + x + ox;
                        if (closed[next] == searchStamp) continue;
                        if (stamp[next] != searchStamp) Initialize(next);
                        int candidate = cost[cell] + (ox == 0 || oz == 0 ? 10 : 14);
                        if (candidate >= cost[next]) continue;
                        cost[next] = candidate; parent[next] = cell;
                        if (heapPosition[next] < 0) Push(next); else BubbleUp(heapPosition[next]);
                    }
            }
            return false;
        }

        private bool BuildPath(int start, float startX, float startZ, List<IntercomNavPoint> output)
        {
            int count = 0, cell = goalCell;
            while (cell != start && count < Count)
            { reversePath[count++] = cell; cell = parent[cell]; if (cell < 0) return false; }
            if (cell != start) return false;
            reversePath[count++] = start;
            float x = startX, z = startZ;
            int cursor = count - 1;
            while (cursor > 0)
            {
                int chosen = -1;
                for (int i = 0; i < cursor; i++)
                {
                    IntercomNavPoint p = Center(reversePath[i] % Width, reversePath[i] / Width);
                    if (SegmentClear(x, z, p.X, p.Z)) { chosen = i; break; }
                }
                if (chosen < 0) { output.Clear(); return false; }
                IntercomNavPoint next = Center(reversePath[chosen] % Width, reversePath[chosen] / Width);
                output.Add(next); x = next.X; z = next.Z; cursor = chosen;
            }
            return output.Count > 0;
        }

        private void Initialize(int cell)
        { stamp[cell] = searchStamp; cost[cell] = int.MaxValue; parent[cell] = -1; heapPosition[cell] = -1; }
        private int Heuristic(int cell)
        {
            int x = Math.Abs(cell % Width - goalCell % Width), z = Math.Abs(cell / Width - goalCell / Width);
            return 10 * Math.Max(x, z) + 4 * Math.Min(x, z);
        }
        private bool Before(int a, int b)
        {
            int ah = Heuristic(a), bh = Heuristic(b), af = cost[a] + ah, bf = cost[b] + bh;
            return af < bf || (af == bf && (ah < bh || (ah == bh && a < b)));
        }
        private void Push(int cell)
        { heap[heapCount] = cell; heapPosition[cell] = heapCount; BubbleUp(heapCount++); }
        private void BubbleUp(int position)
        {
            while (position > 0)
            {
                int up = (position - 1) / 2;
                if (!Before(heap[position], heap[up])) break;
                Swap(position, up); position = up;
            }
        }
        private int Pop()
        {
            int result = heap[0]; heapPosition[result] = -1; heapCount--;
            if (heapCount <= 0) return result;
            heap[0] = heap[heapCount]; heapPosition[heap[0]] = 0;
            int at = 0;
            while (at * 2 + 1 < heapCount)
            {
                int child = at * 2 + 1;
                if (child + 1 < heapCount && Before(heap[child + 1], heap[child])) child++;
                if (!Before(heap[child], heap[at])) break;
                Swap(at, child); at = child;
            }
            return result;
        }
        private void Swap(int a, int b)
        { int value = heap[a]; heap[a] = heap[b]; heap[b] = value; heapPosition[heap[a]] = a; heapPosition[heap[b]] = b; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool OnEdge(float value) => Math.Abs(value - Math.Round(value)) < .000001d;
    }
}
