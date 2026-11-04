// Cohesive deterministic feature models; no scene components or Unity dependencies.
using System;
using System.Collections.Generic;

namespace IntercomMvp
{
    #region IntercomOcclusionGeometry
    /// <summary>Immutable mesh BVH. A hit must be on a real triangle along a finite camera-to-player segment.</summary>
    internal sealed class IntercomOcclusionGeometry
    {
        internal struct Point
        {
            internal float x, y, z;
            internal Point(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
            internal float Axis(int axis) => axis == 0 ? x : axis == 1 ? y : z;
            public static Point operator -(Point a, Point b) => new Point(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        private struct Box
        {
            internal Point min, max;
            internal void Include(Point p)
            {
                min = new Point(Math.Min(min.x, p.x), Math.Min(min.y, p.y), Math.Min(min.z, p.z));
                max = new Point(Math.Max(max.x, p.x), Math.Max(max.y, p.y), Math.Max(max.z, p.z));
            }
            internal bool Intersects(Point start, Point delta)
            {
                double near = 0d, far = 1d;
                for (int axis = 0; axis < 3; axis++)
                {
                    double d = delta.Axis(axis), s = start.Axis(axis);
                    double lo = min.Axis(axis) - 0.000001d, hi = max.Axis(axis) + 0.000001d;
                    if (Math.Abs(d) < 1e-12d) { if (s < lo || s > hi) return false; continue; }
                    double a = (lo - s) / d, b = (hi - s) / d;
                    near = Math.Max(near, Math.Min(a, b)); far = Math.Min(far, Math.Max(a, b));
                    if (near > far) return false;
                }
                return true;
            }
        }

        private struct Triangle { internal Point a, b, c; internal Box bounds; }
        private struct Node { internal Box bounds; internal int start, count, left, right; }
        private sealed class CentroidComparer : IComparer<int>
        {
            internal Triangle[] triangles;
            internal int axis;
            public int Compare(int a, int b)
            {
                Box x = triangles[a].bounds, y = triangles[b].bounds;
                return (x.min.Axis(axis) + x.max.Axis(axis)).CompareTo(y.min.Axis(axis) + y.max.Axis(axis));
            }
        }

        private readonly Triangle[] triangles;
        private readonly int[] order;
        private readonly Node[] nodes;
        private int nodeCount;
        internal int TriangleCount => triangles.Length;

        internal IntercomOcclusionGeometry(Point[] vertices, int[] indices)
        {
            if (vertices == null || indices == null || indices.Length % 3 != 0)
                throw new ArgumentException("Occlusion geometry requires a triangle mesh.");
            triangles = new Triangle[indices.Length / 3]; order = new int[triangles.Length];
            nodes = new Node[Math.Max(1, triangles.Length * 2)];
            for (int i = 0; i < triangles.Length; i++)
            {
                Point a = vertices[indices[i * 3]], b = vertices[indices[i * 3 + 1]], c = vertices[indices[i * 3 + 2]];
                if (!Finite(a) || !Finite(b) || !Finite(c)) throw new ArgumentException("Non-finite occlusion vertex.");
                Box bounds = new Box { min = a, max = a }; bounds.Include(b); bounds.Include(c);
                triangles[i] = new Triangle { a = a, b = b, c = c, bounds = bounds }; order[i] = i;
            }
            if (triangles.Length > 0) Build(0, triangles.Length, new CentroidComparer { triangles = triangles });
        }

        // Arrays are shared for all instances of a mesh. Build only on scenery
        // refresh. Balanced leaves bound traversal; no allocations while sampling.
        private int Build(int start, int count, CentroidComparer comparer)
        {
            int index = nodeCount++;
            Box bounds = triangles[order[start]].bounds;
            for (int i = start + 1; i < start + count; i++)
            { Box part = triangles[order[i]].bounds; bounds.Include(part.min); bounds.Include(part.max); }
            Node node = new Node { bounds = bounds, start = start, count = count };
            if (count > 8)
            {
                Point size = bounds.max - bounds.min;
                comparer.axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                Array.Sort(order, start, count, comparer);
                int half = count / 2;
                node.left = Build(start, half, comparer); node.right = Build(start + half, count - half, comparer);
                node.count = 0;
            }
            nodes[index] = node;
            return index;
        }

        internal bool Intersects(Point start, Point end)
        {
            if (triangles.Length == 0 || !Finite(start) || !Finite(end)) return false;
            return IntersectsNode(0, start, end - start);
        }

        private bool IntersectsNode(int index, Point start, Point delta)
        {
            Node node = nodes[index];
            if (!node.bounds.Intersects(start, delta)) return false;
            if (node.count == 0) return IntersectsNode(node.left, start, delta) || IntersectsNode(node.right, start, delta);
            for (int i = node.start; i < node.start + node.count; i++)
            {
                Triangle triangle = triangles[order[i]];
                if (IntersectsTriangle(start, delta, triangle.a, triangle.b, triangle.c)) return true;
            }
            return false;
        }

        // Double sided, because authored shells may use a single roof surface.
        // Parameter t is unchanged by a renderer's nonuniform inverse transform.
        internal static bool IntersectsTriangle(Point start, Point delta, Point a, Point b, Point c)
        {
            Point ab = b - a, ac = c - a;
            double px = (double)delta.y * ac.z - (double)delta.z * ac.y;
            double py = (double)delta.z * ac.x - (double)delta.x * ac.z;
            double pz = (double)delta.x * ac.y - (double)delta.y * ac.x;
            double det = ab.x * px + ab.y * py + ab.z * pz;
            if (Math.Abs(det) < 1e-12d) return false;
            Point fromA = start - a;
            double u = (fromA.x * px + fromA.y * py + fromA.z * pz) / det;
            if (u < -1e-7d || u > 1d + 1e-7d) return false;
            double qx = (double)fromA.y * ab.z - (double)fromA.z * ab.y;
            double qy = (double)fromA.z * ab.x - (double)fromA.x * ab.z;
            double qz = (double)fromA.x * ab.y - (double)fromA.y * ab.x;
            double v = (delta.x * qx + delta.y * qy + delta.z * qz) / det;
            if (v < -1e-7d || u + v > 1d + 1e-7d) return false;
            double t = (ac.x * qx + ac.y * qy + ac.z * qz) / det;
            return t >= 0d && t <= 1d;
        }

        private static bool Finite(Point p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x)
            && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    }
    #endregion

    #region IntercomOcclusionMath
    /// <summary>Conservative camera-space projection for sample placement; never a final occlusion decision.</summary>
    public static class IntercomOcclusionMath
    {
        public struct Projection
        {
            public float minX, maxX, minY, maxY, nearDepth, farDepth;
        }

        // Camera axes use positive Z forwards. HalfX/Y/Z enclose all eight
        // corners of a renderer's world AABB in those axes. Projection can
        // overestimate a silhouette, but cannot leave a gap between ray samples.
        public static bool ProjectBox(float x, float y, float z, float halfX, float halfY, float halfZ,
            bool orthographic, float verticalHalfSize, float aspect, float nearClip, out Projection result)
        {
            result = default;
            if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(halfX) || !Finite(halfY) || !Finite(halfZ)
                || !Finite(verticalHalfSize) || !Finite(aspect) || !Finite(nearClip)) return false;
            halfX = Math.Abs(halfX); halfY = Math.Abs(halfY); halfZ = Math.Abs(halfZ);
            float near = Math.Max(0.001f, nearClip);
            float front = z - halfZ, back = z + halfZ;
            if (back < near) return false;
            float sy = Math.Max(0.0001f, verticalHalfSize);
            float sx = sy * Math.Max(0.0001f, aspect);
            result.nearDepth = Math.Max(front, near);
            result.farDepth = back;
            if (orthographic)
            {
                result.minX = (x - halfX) / sx; result.maxX = (x + halfX) / sx;
                result.minY = (y - halfY) / sy; result.maxY = (y + halfY) / sy;
            }
            else
            {
                // Here verticalHalfSize is tan(vertical FOV / 2), not metres.
                // Clipping to the near plane also covers boxes crossing it.
                ProjectInterval(x - halfX, x + halfX, result.nearDepth, back, sx, out result.minX, out result.maxX);
                ProjectInterval(y - halfY, y + halfY, result.nearDepth, back, sy, out result.minY, out result.maxY);
            }
            return true;
        }

        public static bool Blocks(Projection obstacle, Projection player)
        {
            return obstacle.nearDepth <= player.farDepth
                && obstacle.maxX >= player.minX && obstacle.minX <= player.maxX
                && obstacle.maxY >= player.minY && obstacle.minY <= player.maxY;
        }

        /// <summary>Legacy broad-phase coverage estimate. A real finite mesh-ray test is required before fading.</summary>
        public static bool BlocksCore(Projection obstacle, Projection core, float coverageThreshold)
        {
            if (!Finite(coverageThreshold) || obstacle.nearDepth >= core.nearDepth - 0.01f) return false;
            float centerX = (core.minX + core.maxX) * 0.5f;
            float centerY = (core.minY + core.maxY) * 0.5f;
            if (centerX < obstacle.minX || centerX > obstacle.maxX
                || centerY < obstacle.minY || centerY > obstacle.maxY) return false;
            float area = (core.maxX - core.minX) * (core.maxY - core.minY);
            if (area <= 0.000001f) return false;
            float width = Math.Max(0f, Math.Min(core.maxX, obstacle.maxX) - Math.Max(core.minX, obstacle.minX));
            float height = Math.Max(0f, Math.Min(core.maxY, obstacle.maxY) - Math.Max(core.minY, obstacle.minY));
            return width * height / area >= Math.Max(0.1f, Math.Min(0.9f, coverageThreshold));
        }

        private static void ProjectInterval(float low, float high, float near, float far, float scale,
            out float minimum, out float maximum)
        {
            float a = low / (near * scale), b = low / (far * scale);
            float c = high / (near * scale), d = high / (far * scale);
            minimum = Math.Min(Math.Min(a, b), Math.Min(c, d));
            maximum = Math.Max(Math.Max(a, b), Math.Max(c, d));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    #endregion

    #region IntercomOcclusionState
    /// <summary>Acquisition/release hysteresis and frame-rate independent opacity.</summary>
    internal sealed class IntercomOcclusionState
    {
        internal bool Fading { get; private set; }
        internal float Opacity { get; private set; } = 1f;
        private float blockedSince = -1f, lastBlocked = float.NegativeInfinity;

        internal void Update(bool blocked, float now, float deltaTime, float obstructionDelay,
            float restoreDelay, float fadeSeconds, float ghostOpacity)
        {
            if (blocked)
            {
                lastBlocked = now;
                if (blockedSince < 0f) blockedSince = now;
                if (now - blockedSince >= Math.Max(0f, obstructionDelay)) Fading = true;
            }
            else
            {
                blockedSince = -1f;
                if (now - lastBlocked >= Math.Max(0f, restoreDelay)) Fading = false;
            }
            float desired = Fading ? Math.Max(0.1f, Math.Min(0.75f, ghostOpacity)) : 1f;
            float step = Math.Max(0f, deltaTime) / Math.Max(0.01f, fadeSeconds);
            Opacity = Opacity < desired ? Math.Min(desired, Opacity + step) : Math.Max(desired, Opacity - step);
        }

        internal void Reset()
        { Fading = false; Opacity = 1f; blockedSince = -1f; lastBlocked = float.NegativeInfinity; }
    }
    #endregion
}
