using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IntercomMvp
{
    /// <summary>Fades only meshes intersecting actual camera-to-torso visibility rays.</summary>
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

        private sealed class Part
        {
            internal Renderer renderer;
            internal IntercomOcclusionGeometry geometry;
        }

        private sealed class Entry
        {
            internal Part[] parts;
            internal readonly IntercomOcclusionState state = new IntercomOcclusionState();
        }

        private const int SampleCount = 9;
        private readonly Vector3[] sampleStarts = new Vector3[SampleCount];
        private readonly Vector3[] sampleEnds = new Vector3[SampleCount];
        private readonly Ray[] sampleRays = new Ray[SampleCount];
        private readonly float[] sampleDistances = new float[SampleCount];
        private readonly Dictionary<Mesh, IntercomOcclusionGeometry> geometryCache = new Dictionary<Mesh, IntercomOcclusionGeometry>();
        private readonly HashSet<Mesh> usedMeshes = new HashSet<Mesh>();
        private readonly List<Mesh> staleMeshes = new List<Mesh>();
        private readonly HashSet<Renderer> retainedRenderers = new HashSet<Renderer>();
        private readonly List<Entry> entries = new List<Entry>();
        private Camera view;
        private Transform cachedTarget;
        private Renderer[] playerRenderers;
        private CharacterController capsule;
        private uint markerRevision;
        private uint visibilityEpoch;
        private int updatedFrame = -1;
        private bool refreshRequested = true;
        private bool geometryRefreshRequested;
        public int CacheRebuildCount { get; private set; }
        public int GeometryBuildCount { get; private set; }

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
            IntercomOcclusionFade.ReleaseUnused(GetInstanceID(), null);
            entries.Clear();
            geometryCache.Clear();
            retainedRenderers.Clear();
            usedMeshes.Clear(); staleMeshes.Clear();
            cachedTarget = null;
        }

        private void OnDestroy() { RestoreAll(); IntercomOcclusionFade.ReleaseUnused(GetInstanceID(), null); }
        private void SceneLoaded(Scene scene, LoadSceneMode mode) { refreshRequested = true; }
        private void SceneUnloaded(Scene scene)
        {
            // A persistent camera can temporarily have no target after unload;
            // free native material clones immediately instead of waiting for
            // a later valid target to trigger RebuildCache.
            RestoreAll(); IntercomOcclusionFade.ReleaseUnused(GetInstanceID(), null);
            entries.Clear(); geometryCache.Clear();
            retainedRenderers.Clear(); usedMeshes.Clear(); staleMeshes.Clear();
            refreshRequested = true;
        }

        /// <summary>Call after adding unmarked mesh objects at runtime. Marked buildings register automatically.</summary>
        [ContextMenu("Refresh Scenery")]
        public void RefreshScenery() { refreshRequested = true; }

        /// <summary>Use after changing vertices/triangles of an existing mesh instance.</summary>
        [ContextMenu("Refresh Changed Geometry")]
        public void RefreshChangedGeometry() { geometryRefreshRequested = true; refreshRequested = true; }

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

            if (!PrepareSamples(player)) { RestoreAll(); return; }
            float now = Time.unscaledTime;
            int owner = GetInstanceID();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                int covered = 0;
                float threshold = entry.state.Fading ? minimumCoverage * 0.6f : minimumCoverage;
                int required = Mathf.CeilToInt(SampleCount * Mathf.Clamp(threshold, 0.1f, 0.9f));
                // Every ray is finite and stops in front of the player's core.
                // Bounds only prune work: they can NEVER decide to fade a mesh.
                for (int sample = 0; sample < SampleCount; sample++)
                {
                    bool hit = false;
                    for (int j = 0; j < entry.parts.Length; j++)
                    {
                        Part part = entry.parts[j];
                        if (!Eligible(part.renderer)) continue;
                        if (!part.renderer.bounds.IntersectRay(sampleRays[sample], out float distance)
                            || distance > sampleDistances[sample]) continue;
                        Transform item = part.renderer.transform;
                        if (part.geometry.Intersects(Point(item.InverseTransformPoint(sampleStarts[sample])),
                            Point(item.InverseTransformPoint(sampleEnds[sample])))) { hit = true; break; }
                    }
                    if (hit) covered++;
                    if (covered >= required || covered + SampleCount - sample - 1 < required) break;
                }
                entry.state.Update(covered >= required, now, Time.unscaledDeltaTime,
                    obstructionDelay, restoreDelay, fadeSeconds, ghostOpacity);
                for (int j = 0; j < entry.parts.Length; j++)
                {
                    Renderer renderer = entry.parts[j].renderer;
                    if (entry.state.Opacity < 0.999f && Eligible(renderer))
                        IntercomOcclusionFade.Set(renderer, owner, entry.state.Opacity, fadeVariantTemplate, true);
                    else IntercomOcclusionFade.Release(renderer, owner);
                }
            }
        }

        private bool Eligible(Renderer renderer) => renderer != null && renderer.enabled
            && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy
            && (sceneryLayers.value & view.cullingMask & (1 << renderer.gameObject.layer)) != 0;

        private bool PrepareSamples(IntercomOcclusionMath.Projection core)
        {
            Vector3 forward = view.transform.forward;
            float endDepth = core.nearDepth - 0.025f;
            if (endDepth <= view.nearClipPlane) return false;
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    // Interior samples avoid fading for a grazing boot or hand.
                    float sx = Mathf.Lerp(core.minX, core.maxX, 0.18f + x * 0.32f);
                    float sy = Mathf.Lerp(core.minY, core.maxY, 0.18f + y * 0.32f);
                    Ray ray = view.ViewportPointToRay(new Vector3(sx * 0.5f + 0.5f, sy * 0.5f + 0.5f, 0f));
                    float depth = Vector3.Dot(ray.origin - view.transform.position, forward);
                    float distance = (endDepth - depth) / Mathf.Max(0.001f, Vector3.Dot(ray.direction, forward));
                    int sample = y * 3 + x;
                    sampleRays[sample] = ray;
                    sampleDistances[sample] = distance;
                    sampleStarts[sample] = ray.origin;
                    sampleEnds[sample] = ray.GetPoint(distance);
                }
            return true;
        }

        private static IntercomOcclusionGeometry.Point Point(Vector3 point)
            => new IntercomOcclusionGeometry.Point(point.x, point.y, point.z);

        private void RebuildCache()
        {
            RestoreAll();
            entries.Clear();
            // Marker changes alter membership, not the vertices of every
            // existing wall. Reuse unchanged BVHs instead of copying all mesh
            // arrays and rebuilding their acceleration structures on each refresh.
            if (geometryRefreshRequested) geometryCache.Clear();
            geometryRefreshRequested = false;
            usedMeshes.Clear(); retainedRenderers.Clear();
            CacheRebuildCount++;
            cachedTarget = target;
            capsule = target.GetComponent<CharacterController>();
            playerRenderers = target.GetComponentsInChildren<Renderer>(true);
            Dictionary<Transform, List<Part>> groups = new Dictionary<Transform, List<Part>>();
            // Only cache when topology changes, never a global Find per frame.
            Renderer[] scenery = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < scenery.Length; i++)
            {
                Renderer renderer = scenery[i];
                if (!(renderer is MeshRenderer)) continue;
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
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                // Imported scenery must enable Read/Write. Guessing from a large
                // collider or AABB would reintroduce background-building fades.
                if (mesh == null || !mesh.isReadable) continue;
                usedMeshes.Add(mesh);
                retainedRenderers.Add(renderer);
                if (!geometryCache.TryGetValue(mesh, out IntercomOcclusionGeometry geometry))
                {
                    Vector3[] source = mesh.vertices;
                    IntercomOcclusionGeometry.Point[] vertices = new IntercomOcclusionGeometry.Point[source.Length];
                    for (int v = 0; v < source.Length; v++) vertices[v] = Point(source[v]);
                    geometry = new IntercomOcclusionGeometry(vertices, mesh.triangles);
                    geometryCache.Add(mesh, geometry);
                    GeometryBuildCount++;
                }
                Transform group = marker != null && marker.fadeWholeHierarchy ? marker.transform : renderer.transform;
                if (!groups.TryGetValue(group, out List<Part> parts))
                {
                    parts = new List<Part>(); groups.Add(group, parts);
                }
                parts.Add(new Part { renderer = renderer, geometry = geometry });
            }
            foreach (List<Part> parts in groups.Values) entries.Add(new Entry { parts = parts.ToArray() });
            // A changing hierarchy cannot accumulate old mesh/material caches.
            staleMeshes.Clear();
            foreach (Mesh mesh in geometryCache.Keys)
                if (!usedMeshes.Contains(mesh)) staleMeshes.Add(mesh);
            for (int i = 0; i < staleMeshes.Count; i++) geometryCache.Remove(staleMeshes[i]);
            IntercomOcclusionFade.ReleaseUnused(GetInstanceID(), retainedRenderers);
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
                for (int j = 0; j < entry.parts.Length; j++)
                    IntercomOcclusionFade.Release(entry.parts[j].renderer, owner);
                entry.state.Reset();
            }
        }
    }
}
