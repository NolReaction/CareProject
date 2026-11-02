using UnityEngine;

namespace IntercomMvp
{
    public enum IntercomSurfaceKind { Asphalt = 0, Concrete = 1, Metal = 2, Gravel = 3 }

    /// <summary>Place on a floor collider or its parent. The nearest tag selects the boot sound bank.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomFootstepSurface : MonoBehaviour
    {
        public IntercomSurfaceKind kind;
        [Range(0f, 2f)] public float volume = 1f;
        [Range(0.6f, 1.4f)] public float pitch = 1f;

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
