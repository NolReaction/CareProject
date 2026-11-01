using System;

namespace IntercomMvp
{
    /// <summary>Conservative camera-space projection, shared with executable geometry tests.</summary>
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

        /// <summary>Only sustained, central torso coverage should soften scenery.</summary>
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
}
