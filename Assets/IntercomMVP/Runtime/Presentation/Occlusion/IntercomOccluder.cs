using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Opts scenery into camera visibility checks. Meshes fade independently by default.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomOccluder : MonoBehaviour
    {
        [Tooltip("Disable for ground, background or props which must never fade. A disabled component also excludes this hierarchy.")]
        public bool hideWhenBlocking = true;

        [Tooltip("Opt in only for one building's roof/walls/trim. Leave off for district folders containing crates, barrels or unrelated buildings. Meshes require Read/Write enabled.")]
        public bool fadeWholeHierarchy = false;

        internal static uint Revision { get; private set; }
        private void OnEnable() { Changed(); }
        private void OnDisable() { Changed(); }
        private void OnDestroy() { Changed(); }
        private void OnTransformChildrenChanged()
        {
            // Cosmetic pools are explicitly excluded as a hierarchy. Adding
            // their next puff/shell cannot change which scenery blocks a hero.
            if (enabled && hideWhenBlocking) Changed();
        }
        private void OnTransformParentChanged() { Changed(); }
        private void OnValidate() { Changed(); }
        private static void Changed() { unchecked { Revision++; } }
    }

}
