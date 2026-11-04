using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Marks a generated district so repeating setup preserves all subsequent scene edits.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomDistrictMarker : MonoBehaviour
    {
        public int layoutVersion = 1;
        public int surfaceVersion;
        public Vector2 worldSize = new Vector2(36f, 32f);
    }
}
