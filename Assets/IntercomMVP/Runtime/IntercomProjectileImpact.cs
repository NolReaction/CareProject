using UnityEngine;

namespace IntercomMvp
{
    /// <summary>One resolved contact shared by audio and particles. Explicit nearest surface tags win.</summary>
    public readonly struct IntercomProjectileImpact
    {
        public readonly Collider Collider;
        public readonly Vector3 Point, Normal;
        public readonly bool TargetHit;
        public readonly IntercomSurfaceKind Surface;
        public readonly float Volume, Pitch;

        private IntercomProjectileImpact(Collider collider, Vector3 point, Vector3 normal, bool targetHit,
            IntercomSurfaceKind surface, float volume, float pitch)
        {
            Collider = collider; Point = point; Normal = normal; TargetHit = targetHit;
            Surface = surface; Volume = volume; Pitch = pitch;
        }

        public static IntercomProjectileImpact Resolve(Collider collider, Vector3 point, Vector3 normal, bool targetHit)
        {
            IntercomSurfaceKind fallback = targetHit ? IntercomSurfaceKind.Metal : IntercomSurfaceKind.Concrete;
            IntercomFootstepSurface tag = IntercomFootstepSurface.Find(collider);
            if (tag == null) return new IntercomProjectileImpact(collider, point, normal, targetHit, fallback, 1f, 1f);
            int kind = (int)tag.kind;
            IntercomSurfaceKind surface = kind >= 0 && kind <= (int)IntercomSurfaceKind.Soil ? tag.kind : fallback;
            float volume = Finite(tag.impactVolume) ? Mathf.Clamp(tag.impactVolume, 0f, 2f) : 1f;
            float pitch = Finite(tag.impactPitch) ? Mathf.Clamp(tag.impactPitch, 0.6f, 1.4f) : 1f;
            return new IntercomProjectileImpact(collider, point, normal, targetHit, surface, volume, pitch);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
