using System.Collections.Generic;
using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Add to a building root so its roof, walls and trim hide together.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomOccluder : MonoBehaviour
    {
        [Tooltip("Disable for ground, background or objects which must never be hidden. A disabled component also excludes this hierarchy.")]
        public bool hideWhenBlocking = true;

        internal static uint Revision { get; private set; }
        private void OnEnable() { Changed(); }
        private void OnDisable() { Changed(); }
        private void OnDestroy() { Changed(); }
        private void OnTransformChildrenChanged() { Changed(); }
        private void OnTransformParentChanged() { Changed(); }
        private void OnValidate() { Changed(); }
        private static void Changed() { unchecked { Revision++; } }
    }

    // A small lease table avoids one camera restoring a renderer still hidden
    // by another camera. It preserves an existing forceRenderingOff value and
    // never writes Renderer.enabled, GameObject.activeSelf or any material.
    internal static class IntercomOcclusionVisibility
    {
        private sealed class Lease { internal int owners; internal bool original; }
        private static readonly Dictionary<Renderer, Lease> Leases = new Dictionary<Renderer, Lease>();
        internal static uint Epoch { get; private set; }

        internal static void Acquire(Renderer renderer)
        {
            if (renderer == null) return;
            if (!Leases.TryGetValue(renderer, out Lease lease))
            {
                lease = new Lease { original = renderer.forceRenderingOff };
                Leases.Add(renderer, lease);
            }
            lease.owners++;
            renderer.forceRenderingOff = true;
        }

        internal static void Release(Renderer renderer)
        {
            if (ReferenceEquals(renderer, null) || !Leases.TryGetValue(renderer, out Lease lease)) return;
            if (--lease.owners > 0) return;
            if (renderer != null) renderer.forceRenderingOff = lease.original;
            Leases.Remove(renderer);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (KeyValuePair<Renderer, Lease> pair in Leases)
                if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value.original;
            Leases.Clear();
            unchecked { Epoch++; }
        }
    }
}
