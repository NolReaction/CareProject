using UnityEngine;

namespace IntercomMvp
{
    public enum IntercomAnchorRole { Zone, Building, Route, Cover, Prop, Boundary, PlayerSpawn, EnemySpawn, Loot, Exit, Target }

    /// <summary>Stable layout identity. Replace the Visual children while retaining this anchor and collision setup.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomWorldAnchor : MonoBehaviour
    {
        public string anchorId;
        public IntercomAnchorRole role;
        public Vector2 footprint = Vector2.one;
        [TextArea] public string designerNote = "Placeholder: replace visual children with your model.";

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.9f, 0.85f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.03f,
                new Vector3(footprint.x, 0.06f, footprint.y));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, anchorId + " / " + role);
#endif
        }
    }
}
