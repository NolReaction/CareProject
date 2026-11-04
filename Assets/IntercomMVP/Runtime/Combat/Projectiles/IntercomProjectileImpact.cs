using UnityEngine;

namespace IntercomMvp
{
    /// <summary>One resolved contact shared by audio and particles. Explicit nearest surface tags win.</summary>
    public readonly struct IntercomProjectileImpact
    {
        public readonly Collider Collider;
        public readonly Vector3 Point, Normal;
        public readonly bool TargetHit;
        public readonly bool RobotHit;
        public readonly IntercomSurfaceKind Surface;
        public readonly float Volume, Pitch;

        private IntercomProjectileImpact(Collider collider, Vector3 point, Vector3 normal, bool targetHit,
            IntercomSurfaceKind surface, float volume, float pitch, bool robotHit = false)
        {
            Collider = collider; Point = point; Normal = normal; TargetHit = targetHit;
            Surface = surface; Volume = volume; Pitch = pitch;
            RobotHit = robotHit;
        }

        public static IntercomProjectileImpact Resolve(Collider collider, Vector3 point, Vector3 normal, bool targetHit)
        {
            // Accepted damage alone does not identify a robot: crates and
            // targets also take damage. Robot character comes from an explicit tag.
            IntercomSurfaceKind fallback = IntercomSurfaceKind.Concrete;
            IntercomFootstepSurface tag = IntercomFootstepSurface.Find(collider);
            if (tag == null) return new IntercomProjectileImpact(collider, point, normal, targetHit, fallback, 1f, 1f);
            int kind = (int)tag.kind;
            IntercomSurfaceKind surface = kind >= 0 && kind <= (int)IntercomSurfaceKind.Soil ? tag.kind : fallback;
            float volume = Finite(tag.impactVolume) ? Mathf.Clamp(tag.impactVolume, 0f, 2f) : 1f;
            float pitch = Finite(tag.impactPitch) ? Mathf.Clamp(tag.impactPitch, 0.6f, 1.4f) : 1f;
            return new IntercomProjectileImpact(collider, point, normal, targetHit, surface, volume, pitch,
                tag.impactKind == IntercomImpactKind.Robot);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
