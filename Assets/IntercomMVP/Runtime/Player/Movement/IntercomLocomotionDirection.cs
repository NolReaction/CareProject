using System;

namespace IntercomMvp
{
    /// <summary>Unity-independent horizontal heading projection and angle smoothing.</summary>
    public static class IntercomLocomotionDirection
    {
        public static bool TryProject(float velocityX, float velocityZ, float facingX, float facingZ,
            out float localX, out float localY)
        {
            localX = 0f; localY = 1f;
            if (!Finite(velocityX) || !Finite(velocityZ) || !Finite(facingX) || !Finite(facingZ)) return false;
            double velocityLength = Math.Sqrt((double)velocityX * velocityX + (double)velocityZ * velocityZ);
            double facingLength = Math.Sqrt((double)facingX * facingX + (double)facingZ * facingZ);
            if (velocityLength <= 0.01 || facingLength <= 0.00001) return false;
            double scale = 1.0 / (velocityLength * facingLength);
            // Unity right = cross(up, forward) = (forward.z, 0, -forward.x).
            localX = (float)((velocityX * (double)facingZ - velocityZ * (double)facingX) * scale);
            localY = (float)((velocityX * (double)facingX + velocityZ * (double)facingZ) * scale);
            return true;
        }

        public static void Smooth(float currentX, float currentY, float targetX, float targetY,
            float deltaTime, float smoothTime, out float resultX, out float resultY)
        {
            double current = Math.Atan2(currentX, currentY);
            double desired = Math.Atan2(targetX, targetY);
            double difference = desired - current;
            while (difference > Math.PI) difference -= Math.PI * 2.0;
            while (difference < -Math.PI) difference += Math.PI * 2.0;
            double amount = smoothTime <= 0f ? 1.0 : 1.0 - Math.Exp(-Math.Max(0f, deltaTime) / smoothTime);
            double result = current + difference * amount;
            // Angle interpolation keeps a unit vector even on a 180-degree
            // reversal. Linear XY damping would pass through the undefined centre.
            resultX = (float)Math.Sin(result);
            resultY = (float)Math.Cos(result);
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
