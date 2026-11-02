using UnityEngine;

namespace IntercomMvp
{
    // Keep serialized values stable; the same tag routes foot contacts and impacts.
    public enum IntercomSurfaceKind { Asphalt = 0, Concrete = 1, Metal = 2, Gravel = 3, Wood = 4, Soil = 5 }

    /// <summary>Place on a collider or its parent. The nearest enabled tag routes footsteps and projectile contacts.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomFootstepSurface : MonoBehaviour
    {
        public IntercomSurfaceKind kind;
        [Range(0f, 2f)] public float volume = 1f;
        [Range(0.6f, 1.4f)] public float pitch = 1f;
        [Header("Projectile impact")]
        [Range(0f, 2f)] public float impactVolume = 1f;
        [Range(0.6f, 1.4f)] public float impactPitch = 1f;

        public static IntercomFootstepSurface Find(Collider surface)
        {
            for (Transform current = surface != null ? surface.transform : null;
                current != null; current = current.parent)
            {
                IntercomFootstepSurface tag = current.GetComponent<IntercomFootstepSurface>();
                if (tag != null && tag.isActiveAndEnabled) return tag;
            }
            return null;
        }
    }
}
