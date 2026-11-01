using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IntercomMvp
{
    /// <summary>Fades substantial torso occluders while leaving small foreground overlaps visible.</summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class IntercomCameraOcclusion : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Small extra clearance around the torso core, excluding hands and weapons.")]
        [Min(0f)] public float silhouettePadding = 0.015f;
        [Tooltip("Delay before an unobstructed building returns to full opacity.")]
        [Min(0f)] public float restoreDelay = 0.24f;
        [Range(0.1f, 0.9f)] public float minimumCoverage = 0.45f;
        [Min(0f)] public float obstructionDelay = 0.10f;
        [Min(0.01f)] public float fadeSeconds = 0.20f;
        [Range(0.1f, 0.75f)] public float ghostOpacity = 0.25f;
        [Tooltip("Retains the transparent URP Lit shader variant in a mobile build. Setup assigns this asset.")]
        public Material fadeVariantTemplate;
        [Tooltip("Layer filter for scenery. The target hierarchy is always excluded.")]
        public LayerMask sceneryLayers = ~0;
        [Tooltip("Also discover unmarked scene meshes on startup. For a spawned building, add IntercomOccluder to its root.")]
        public bool includeUnmarkedScenery = false;

        private sealed class Entry
        {
            internal Renderer[] renderers;
            internal bool fading;
            internal float blockedSince = -1f;
            internal float lastBlocked;
            internal float opacity = 1f;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private Camera view;
        private Transform cachedTarget;
        private Renderer[] playerRenderers;
        private CharacterController capsule;
        private uint markerRevision;
        private uint visibilityEpoch;
        private int updatedFrame = -1;
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
                || visibilityEpoch != IntercomOcclusionFade.Epoch)
                RebuildCache();
            // LateUpdate and camera callbacks may both run in one frame.
            // Advance each fade only once; otherwise framerate changes its duration.
            if (updatedFrame == Time.frameCount) return;
            updatedFrame = Time.frameCount;
            if (!TryPlayerBounds(out Bounds fullBounds)) { RestoreAll(); return; }
            Bounds core = fullBounds;
            core.center += Vector3.up * fullBounds.size.y * 0.10f;
            core.size = new Vector3(fullBounds.size.x * 0.75f, fullBounds.size.y * 0.55f, fullBounds.size.z * 0.75f);
            core.Expand(Mathf.Max(0f, silhouettePadding) * 2f);
            if (!Project(core, out IntercomOcclusionMath.Projection player)) { RestoreAll(); return; }

            float now = Time.unscaledTime;
            int owner = GetInstanceID();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                bool blocked = false;
                Bounds groupBounds = default;
                bool haveBounds = false;
                for (int j = 0; j < entry.renderers.Length; j++)
                {
                    Renderer part = entry.renderers[j];
                    if (part == null || !part.enabled || !part.gameObject.activeInHierarchy) continue;
                    if (!haveBounds) { groupBounds = part.bounds; haveBounds = true; }
                    else groupBounds.Encapsulate(part.bounds);
                }
                // Waist-high covers and crates remain visible. Their small
                // overlap with boots is useful depth information to the player.
                bool tallEnough = haveBounds && groupBounds.size.y >= fullBounds.size.y * 0.75f
                    && groupBounds.max.y >= core.center.y;
                float threshold = entry.fading ? minimumCoverage * 0.6f : minimumCoverage;
                if (tallEnough)
                    for (int j = 0; j < entry.renderers.Length; j++)
                    {
                        Renderer renderer = entry.renderers[j];
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                        if ((sceneryLayers.value & view.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                        if (Project(renderer.bounds, out IntercomOcclusionMath.Projection obstacle)
                            && IntercomOcclusionMath.BlocksCore(obstacle, player, threshold))
                        { blocked = true; break; }
                    }
                if (blocked)
                {
                    entry.lastBlocked = now;
                    if (entry.blockedSince < 0f) entry.blockedSince = now;
                    if (now - entry.blockedSince >= Mathf.Max(0f, obstructionDelay)) entry.fading = true;
                }
                else
                {
                    entry.blockedSince = -1f;
                    if (now - entry.lastBlocked >= Mathf.Max(0f, restoreDelay)) entry.fading = false;
                }
                float desired = entry.fading ? Mathf.Clamp(ghostOpacity, 0.1f, 0.75f) : 1f;
                entry.opacity = Mathf.MoveTowards(entry.opacity, desired,
                    Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
                for (int j = 0; j < entry.renderers.Length; j++)
                {
                    Renderer renderer = entry.renderers[j];
                    if (entry.opacity < 0.999f) IntercomOcclusionFade.Set(renderer, owner, entry.opacity, fadeVariantTemplate);
                    else IntercomOcclusionFade.Release(renderer, owner);
                }
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
            visibilityEpoch = IntercomOcclusionFade.Epoch;
            refreshRequested = false;
        }

        private static bool IsProjectile(Transform item)
        {
            for (Transform node = item; node != null; node = node.parent)
                if (node.name == "Intercom Projectile Pool" || node.name == "Intercom Combat FX"
                    || node.name == "Intercom Contact FX") return true;
            return false;
        }

        private bool TryPlayerBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            if (capsule != null && capsule.enabled)
            {
                bounds = capsule.bounds;
                return true;
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

        private void RestoreAll()
        {
            int owner = GetInstanceID();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                for (int j = 0; j < entry.renderers.Length; j++)
                    IntercomOcclusionFade.Release(entry.renderers[j], owner);
                entry.opacity = 1f;
                entry.fading = false;
                entry.blockedSince = -1f;
            }
        }
    }
}
