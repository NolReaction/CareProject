using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Add to a building root so its roof, walls and trim fade together.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomOccluder : MonoBehaviour
    {
        [Tooltip("Disable for ground, background or props which must never fade. A disabled component also excludes this hierarchy.")]
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

}
