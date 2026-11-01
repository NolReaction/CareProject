using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IntercomMvp
{
    /// <summary>Hides foreground scenery immediately when it overlaps the full player silhouette.</summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class IntercomCameraOcclusion : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Extra world-space clearance around the player's full rendered bounds.")]
        [Min(0f)] public float silhouettePadding = 0.08f;
        [Tooltip("Scenery reappears after it has been clear for this many seconds. Hiding is always immediate.")]
        [Min(0f)] public float restoreDelay = 0.18f;
        [Tooltip("Layer filter for scenery. The target hierarchy is always excluded.")]
        public LayerMask sceneryLayers = ~0;
        [Tooltip("Also discover unmarked scene meshes on startup. For a spawned building, add IntercomOccluder to its root.")]
        public bool includeUnmarkedScenery = true;

        private sealed class Entry
        {
            internal Renderer[] renderers;
            internal bool hidden;
            internal uint hiddenEpoch;
            internal float lastBlocked;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private Camera view;
        private Transform cachedTarget;
        private Renderer[] playerRenderers;
        private CharacterController capsule;
        private uint markerRevision;
        private uint visibilityEpoch;
        private bool refreshRequested = true;

        private void OnEnable()
        {
            view = GetComponent<Camera>();
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
            Camera.onPreCull += BeforeBuiltinCamera;
            RenderPipelineManager.beginCameraRendering += BeforeScriptableCamera;
            refreshRequested = true;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            Camera.onPreCull -= BeforeBuiltinCamera;
            RenderPipelineManager.beginCameraRendering -= BeforeScriptableCamera;
            RestoreAll();
            entries.Clear();
            cachedTarget = null;
        }

        private void OnDestroy() { RestoreAll(); }
        private void SceneLoaded(Scene scene, LoadSceneMode mode) { refreshRequested = true; }
        private void SceneUnloaded(Scene scene) { refreshRequested = true; }

        /// <summary>Call after adding unmarked mesh objects at runtime. Marked buildings register automatically.</summary>
        [ContextMenu("Refresh Scenery")]
        public void RefreshScenery() { refreshRequested = true; }

        private void LateUpdate() { UpdateVisibility(); }

        private void BeforeBuiltinCamera(Camera camera)
        {
            if (camera == view) UpdateVisibility();
        }

        private void BeforeScriptableCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == view) UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (!Application.isPlaying) return;
            if (target == null || !target.gameObject.activeInHierarchy || view == null || !view.enabled)
            {
                RestoreAll();
                return;
            }
            if (cachedTarget != target || refreshRequested || markerRevision != IntercomOccluder.Revision
                || visibilityEpoch != IntercomOcclusionVisibility.Epoch)
                RebuildCache();
            if (!TryPlayerBounds(out Bounds playerBounds)) { RestoreAll(); return; }
            playerBounds.Expand(Mathf.Max(0f, silhouettePadding) * 2f);
            if (!Project(playerBounds, out IntercomOcclusionMath.Projection player)) { RestoreAll(); return; }

            float now = Time.unscaledTime;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                bool blocked = false;
                // Each part is tested independently, so a large empty gap
                // between two child meshes does not hide their whole group.
                for (int j = 0; j < entry.renderers.Length; j++)
                {
                    Renderer renderer = entry.renderers[j];
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if ((sceneryLayers.value & view.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                    if (Project(renderer.bounds, out IntercomOcclusionMath.Projection obstacle)
                        && IntercomOcclusionMath.Blocks(obstacle, player)) { blocked = true; break; }
                }
                if (blocked)
                {
                    entry.lastBlocked = now;
                    SetHidden(entry, true);
                }
                else if (entry.hidden && now - entry.lastBlocked >= Mathf.Max(0f, restoreDelay)) SetHidden(entry, false);

                if (entry.hidden)
                    for (int j = 0; j < entry.renderers.Length; j++)
                        if (entry.renderers[j] != null) entry.renderers[j].forceRenderingOff = true;
            }
        }

        private void RebuildCache()
        {
            RestoreAll();
            entries.Clear();
            cachedTarget = target;
            capsule = target.GetComponent<CharacterController>();
            playerRenderers = target.GetComponentsInChildren<Renderer>(true);
            Dictionary<Transform, List<Renderer>> groups = new Dictionary<Transform, List<Renderer>>();
            // Only cache when topology changes, never a global Find per frame.
            Renderer[] scenery = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < scenery.Length; i++)
            {
                Renderer renderer = scenery[i];
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (renderer.transform == target || renderer.transform.IsChildOf(target)) continue;
                if (!renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded) continue;
                IntercomOccluder marker = renderer.GetComponentInParent<IntercomOccluder>(true);
                if (marker != null && (!marker.enabled || !marker.hideWhenBlocking)) continue;
                if (marker == null)
                {
                    if (!includeUnmarkedScenery) continue;
                    // Floor paint is excluded; an equally thin roof above the
                    // player's feet still participates and can hide its house.
                    float floor = capsule != null && capsule.enabled ? capsule.bounds.min.y : target.position.y;
                    if (renderer.bounds.size.y < 0.08f && renderer.bounds.max.y <= floor + 0.08f) continue;
                }
                if (IsProjectile(renderer.transform)) continue;
                Transform group = marker != null ? marker.transform : renderer.transform;
                if (!groups.TryGetValue(group, out List<Renderer> parts))
                {
                    parts = new List<Renderer>(); groups.Add(group, parts);
                }
                parts.Add(renderer);
            }
            foreach (List<Renderer> parts in groups.Values) entries.Add(new Entry { renderers = parts.ToArray() });
            markerRevision = IntercomOccluder.Revision;
            visibilityEpoch = IntercomOcclusionVisibility.Epoch;
            refreshRequested = false;
        }

        private static bool IsProjectile(Transform item)
        {
            for (Transform node = item; node != null; node = node.parent)
                if (node.name == "Intercom Projectile Pool") return true;
            return false;
        }

        private bool TryPlayerBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            if (capsule != null && capsule.enabled)
            {
                bounds = capsule.bounds;
                found = true;
            }
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                Renderer renderer = playerRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private bool Project(Bounds bounds, out IntercomOcclusionMath.Projection projection)
        {
            Transform cameraTransform = view.transform;
            Vector3 delta = bounds.center - cameraTransform.position;
            Vector3 right = cameraTransform.right, up = cameraTransform.up, forward = cameraTransform.forward;
            Vector3 extents = bounds.extents;
            float halfX = AbsDot(right, extents), halfY = AbsDot(up, extents), halfZ = AbsDot(forward, extents);
            float verticalHalf = view.orthographic ? view.orthographicSize : Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return IntercomOcclusionMath.ProjectBox(Vector3.Dot(delta, right), Vector3.Dot(delta, up), Vector3.Dot(delta, forward),
                halfX, halfY, halfZ, view.orthographic, verticalHalf, view.aspect, view.nearClipPlane, out projection);
        }

        private static float AbsDot(Vector3 axis, Vector3 extents)
            => Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;

        private static void SetHidden(Entry entry, bool hide)
        {
            if (entry.hidden == hide) return;
            entry.hidden = hide;
            if (hide) entry.hiddenEpoch = IntercomOcclusionVisibility.Epoch;
            for (int i = 0; i < entry.renderers.Length; i++)
            {
                if (hide) IntercomOcclusionVisibility.Acquire(entry.renderers[i]);
                else if (entry.hiddenEpoch == IntercomOcclusionVisibility.Epoch)
                    IntercomOcclusionVisibility.Release(entry.renderers[i]);
            }
        }

        private void RestoreAll()
        {
            for (int i = 0; i < entries.Count; i++) SetHidden(entries[i], false);
        }
    }
}
