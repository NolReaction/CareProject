using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    /// <summary>Cosmetic, bounded mechanical bursts. Native resources are prepared before deaths, never on Emit.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class IntercomRobotDeathEffects : MonoBehaviour
    {
        [Header("Shared appearance")]
        public Material gearMaterial;
        public Material outlineMaterial;
        public Material flashMaterial;
        public IntercomNpcDefinition definition;
        [Tooltip("Optional static chunk template. Meshes are copied by reference into the warm pool, never baked on a death.")]
        public GameObject breakupTemplate;
        [Header("Bounded mobile presentation")]
        [Range(1, 8)] public int maximumBursts = 8;
        [Range(3, 6)] public int gearsPerBurst = 5;
        [Range(0, 3)] public int maximumChunkBursts = 2;
        [Range(2, 8)] public int breakupEvery = 3;
        [Range(.8f, 2f)] public float scrapLifetime = 1.45f;
        [Range(.7f, 1.8f)] public float chunkLifetime = 1.3f;

        private const int MaxChunks = 6;
        private sealed class Piece
        {
            internal Transform transform;
            internal Vector3 center, extents, restPosition, restScale, position, velocity, spin;
            internal Quaternion restRotation, rotation;
            internal float age, life, floor;
            internal bool active;
        }
        private sealed class Burst
        {
            internal GameObject root;
            internal Piece[] gears;
            internal Transform pop, ring;
            internal Transform[] puffs;
            internal float age;
            internal Vector3 center;
            internal bool active;
        }
        private sealed class ChunkSet
        {
            internal GameObject root;
            internal Piece[] pieces;
            internal bool active;
        }
        private struct ChunkSource
        {
            internal Mesh mesh;
            internal Material material;
            internal Vector3 position, scale;
            internal Quaternion rotation;
        }

        private GameObject poolRoot;
        private Mesh[] meshes;
        private Burst[] bursts;
        private ChunkSet[] chunks;
        private ChunkSource[] chunkSources;
        private int preparedBursts, preparedChunks, nextBurst, nextChunk;
        private bool focused = true, paused;
        private uint random;
        public int TotalBursts { get; private set; }
        public int PreparedBursts => preparedBursts;
        public int PreparedChunkBursts => preparedChunks;
        public int ActivePieces
        {
            get
            {
                int count = 0;
                if (bursts != null) for (int i = 0; i < preparedBursts; i++)
                    for (int p = 0; p < bursts[i].gears.Length; p++) if (bursts[i].gears[p].active) count++;
                if (chunks != null) for (int i = 0; i < preparedChunks; i++)
                    for (int p = 0; p < chunks[i].pieces.Length; p++) if (chunks[i].pieces[p].active) count++;
                return count;
            }
        }

        private void Awake()
        {
            focused = Application.isFocused;
            PrepareOne();
        }
        private void OnEnable() { focused = Application.isFocused; paused = false; }
        private void Update()
        {
            if (!focused || paused || Time.timeScale <= 0f) return;
            // Initial menu frames amortize native GameObject/renderer construction.
            PrepareOne();
            float dt = Time.deltaTime;
            if (!Finite(dt) || dt <= 0f) return;
            Tick(dt, Mathf.Min(dt, .05f));
        }

        private bool PrepareResources()
        {
            if (poolRoot != null) return true;
            if (gearMaterial == null || outlineMaterial == null || flashMaterial == null) return false;
            if (breakupTemplate == null && definition != null) breakupTemplate = definition.breakupPrefab;
            bursts = new Burst[Mathf.Clamp(maximumBursts, 1, 8)];
            chunks = new ChunkSet[Mathf.Clamp(maximumChunkBursts, 0, 3)];
            meshes = new[] { RingMesh("Scrap cog", 48, .32f, .16f, 0), RingMesh("Scrap nut", 24, .42f, .36f, 1),
                BoltMesh(), RingMesh("Mechanical pop ring", 32, .80f, .025f, 2), PopMesh(), PuffMesh() };
            poolRoot = new GameObject("Robot scrap pool - cosmetic, no physics");
            poolRoot.transform.SetParent(transform, true);
            CacheChunkSources();
            return true;
        }
        private void CacheChunkSources()
        {
            if (breakupTemplate == null) { chunkSources = new ChunkSource[0]; return; }
            MeshFilter[] filters = breakupTemplate.GetComponentsInChildren<MeshFilter>(true);
            var source = new List<ChunkSource>(MaxChunks);
            for (int i = 0; i < filters.Length && source.Count < MaxChunks; i++)
            {
                MeshFilter filter = filters[i]; MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || renderer.sharedMaterial == null) continue;
                Transform t = filter.transform;
                source.Add(new ChunkSource { mesh = filter.sharedMesh, material = renderer.sharedMaterial,
                    position = breakupTemplate.transform.InverseTransformPoint(t.position),
                    rotation = Quaternion.Inverse(breakupTemplate.transform.rotation) * t.rotation, scale = t.localScale });
            }
            chunkSources = source.ToArray();
        }
        private void PrepareOne()
        {
            if (!PrepareResources()) return;
            if (preparedBursts < bursts.Length)
            {
                var b = new Burst();
                b.root = Child("Mechanical burst " + preparedBursts, poolRoot.transform);
                b.gears = new Piece[Mathf.Clamp(gearsPerBurst, 3, 6)];
                for (int p = 0; p < b.gears.Length; p++) b.gears[p] = CreatePiece("Scrap " + p, b.root.transform, meshes[p % 3], gearMaterial, true);
                b.pop = RenderObject("Ink pop", b.root.transform, meshes[4], flashMaterial);
                b.ring = RenderObject("Expanding ring", b.root.transform, meshes[3], flashMaterial);
                b.puffs = new Transform[3];
                for (int p = 0; p < b.puffs.Length; p++) b.puffs[p] = RenderObject("Soft mechanical puff " + p, b.root.transform, meshes[5], outlineMaterial);
                b.root.SetActive(false); bursts[preparedBursts++] = b;
                return;
            }
            if (preparedChunks < chunks.Length && chunkSources.Length > 0)
            {
                var set = new ChunkSet(); set.root = Child("Coarse breakup " + preparedChunks, poolRoot.transform);
                set.pieces = new Piece[chunkSources.Length];
                for (int i = 0; i < chunkSources.Length; i++)
                {
                    ChunkSource s = chunkSources[i];
                    Piece p = CreatePiece("Robot part " + i, set.root.transform, s.mesh, s.material, false);
                    p.restPosition = s.position; p.restRotation = s.rotation; p.restScale = s.scale; set.pieces[i] = p;
                }
                set.root.SetActive(false); chunks[preparedChunks++] = set;
            }
        }

        /// <summary>Returns false only when this optional presentation is unready or invalid. No native allocation occurs here.</summary>
        public bool Emit(Vector3 position, Quaternion rotation, uint lifeSeed)
        {
            if (!isActiveAndEnabled || !focused || paused || Time.timeScale <= 0f || preparedBursts == 0
                || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return false;
            random = lifeSeed == 0 ? 0x5a17b9d3u : lifeSeed;
            Burst b = bursts[nextBurst++ % preparedBursts]; ResetBurst(b);
            b.active = true; b.age = 0f; b.center = position + Vector3.up * .64f; b.root.SetActive(true);
            for (int i = 0; i < b.gears.Length; i++)
            {
                float angle = (i + Next() * .40f) * Mathf.PI * 2f / b.gears.Length;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float size = .075f + Next() * .06f;
                Activate(b.gears[i], b.center + radial * .06f, Quaternion.Euler(Next() * 90f, Next() * 360f, Next() * 90f),
                    Vector3.one * size, radial * (.9f + Next() * .9f) + Vector3.up * (2.1f + Next() * 1.2f), position.y,
                    Safe(scrapLifetime, 1.45f, .8f, 2f));
            }
            TotalBursts++;
            if (preparedChunks > 0 && TotalBursts % Mathf.Clamp(breakupEvery, 2, 8) == 0)
            {
                ChunkSet set = chunks[nextChunk++ % preparedChunks]; ResetChunks(set); set.active = true; set.root.SetActive(true);
                for (int i = 0; i < set.pieces.Length; i++)
                {
                    Piece p = set.pieces[i]; Vector3 radial = rotation * new Vector3(p.restPosition.x, 0f, p.restPosition.z);
                    if (radial.sqrMagnitude < .01f) radial = new Vector3(Mathf.Cos(i * 2.4f), 0f, Mathf.Sin(i * 2.4f));
                    radial.Normalize();
                    Activate(p, position + rotation * p.restPosition, rotation * p.restRotation, p.restScale,
                        radial * (.65f + Next() * .7f) + Vector3.up * (1.4f + Next() * .8f), position.y,
                        Safe(chunkLifetime, 1.3f, .7f, 1.8f));
                }
            }
            PresentBurst(b); return true;
        }
        private void Activate(Piece p, Vector3 position, Quaternion rotation, Vector3 scale, Vector3 velocity, float floor, float lifetime)
        {
            p.position = position; p.rotation = rotation; p.restScale = scale; p.velocity = velocity;
            p.spin = new Vector3((Next() - .5f) * 1000f, (Next() - .5f) * 900f, (Next() - .5f) * 1000f);
            p.floor = floor + .006f; p.age = 0f; p.life = lifetime; p.active = true;
            p.transform.SetPositionAndRotation(position, rotation); p.transform.localScale = scale; p.transform.gameObject.SetActive(true);
        }
        private float Next()
        {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return (random & 0x00ffffffu) / 16777216f;
        }
        private void Tick(float elapsed, float motionDelta)
        {
            for (int i = 0; i < preparedBursts; i++)
            {
                Burst b = bursts[i]; if (!b.active) continue; b.age += elapsed; bool alive = false;
                for (int p = 0; p < b.gears.Length; p++) { Advance(b.gears[p], elapsed, motionDelta); alive |= b.gears[p].active; }
                PresentBurst(b);
                if (!alive && b.age > .36f) ResetBurst(b);
            }
            for (int i = 0; i < preparedChunks; i++)
            {
                ChunkSet set = chunks[i]; if (!set.active) continue; bool alive = false;
                for (int p = 0; p < set.pieces.Length; p++) { Advance(set.pieces[p], elapsed, motionDelta); alive |= set.pieces[p].active; }
                if (!alive) ResetChunks(set);
            }
        }
        private static void Advance(Piece p, float elapsed, float dt)
        {
            if (!p.active) return;
            // Timers use real scaled frame time; only ballistics are bounded after a hitch.
            p.age += elapsed;
            if (p.age >= p.life) { p.active = false; p.transform.gameObject.SetActive(false); return; }
            p.velocity.y -= 9.5f * dt; p.position += p.velocity * dt;
            p.rotation = Quaternion.Euler(p.spin.x * dt, p.spin.y * dt, p.spin.z * dt) * p.rotation;
            float shrink = Mathf.Clamp01((p.life - p.age) / .30f);
            Vector3 scale = p.restScale * (shrink * shrink * (3f - 2f * shrink));
            Vector3 right = p.rotation * new Vector3(1f, 0f, 0f), up = p.rotation * Vector3.up, forward = p.rotation * Vector3.forward;
            float extent = Mathf.Abs(right.y * p.extents.x * scale.x) + Mathf.Abs(up.y * p.extents.y * scale.y)
                + Mathf.Abs(forward.y * p.extents.z * scale.z);
            Vector3 c = new Vector3(p.center.x * scale.x, p.center.y * scale.y, p.center.z * scale.z);
            float bottom = p.position.y + (p.rotation * c).y - extent;
            if (bottom < p.floor)
            {
                p.position.y += p.floor - bottom;
                if (p.velocity.y < 0f) p.velocity.y = -p.velocity.y * .23f;
                p.velocity.x *= Mathf.Max(0f, 1f - dt * 7f); p.velocity.z *= Mathf.Max(0f, 1f - dt * 7f);
                p.spin *= Mathf.Max(0f, 1f - dt * 8f);
            }
            p.transform.SetPositionAndRotation(p.position, p.rotation); p.transform.localScale = scale;
        }
        private static void PresentBurst(Burst b)
        {
            float t = b.age;
            b.pop.gameObject.SetActive(t < .13f);
            if (t < .13f)
            {
                b.pop.position = b.center; b.pop.rotation = Quaternion.identity;
                float s = .13f + .34f * Mathf.Sin(Mathf.Clamp01(t / .13f) * Mathf.PI);
                b.pop.localScale = new Vector3(s, s, s);
            }
            b.ring.gameObject.SetActive(t < .30f);
            if (t < .30f)
            {
                b.ring.position = b.center - Vector3.up * .36f;
                float s = .18f + t * 2.3f; b.ring.localScale = new Vector3(s, Mathf.Max(.05f, 1f - t / .30f), s);
            }
            for (int i = 0; i < b.puffs.Length; i++)
            {
                Transform p = b.puffs[i]; p.gameObject.SetActive(t < .36f);
                if (t >= .36f) continue;
                float angle = i * 2.0944f, growth = t / .36f;
                p.position = b.center + new Vector3(Mathf.Cos(angle), .3f, Mathf.Sin(angle)) * (t * .72f);
                p.localScale = Vector3.one * ((.08f + growth * .10f) * (1f - growth));
            }
        }
        public void Clear()
        {
            if (bursts != null) for (int i = 0; i < preparedBursts; i++) ResetBurst(bursts[i]);
            if (chunks != null) for (int i = 0; i < preparedChunks; i++) ResetChunks(chunks[i]);
            nextBurst = nextChunk = 0;
        }
        private static void ResetBurst(Burst b)
        {
            b.active = false; b.root.SetActive(false);
            for (int i = 0; i < b.gears.Length; i++) b.gears[i].active = false;
        }
        private static void ResetChunks(ChunkSet set)
        {
            set.active = false; set.root.SetActive(false);
            for (int i = 0; i < set.pieces.Length; i++) set.pieces[i].active = false;
        }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) Clear(); }
        private void OnApplicationPause(bool value) { paused = value; if (value) Clear(); }
        private void OnDisable() { Clear(); }
        private void OnDestroy()
        {
            Clear();
            if (poolRoot != null) Destroy(poolRoot);
            if (meshes != null) for (int i = 0; i < meshes.Length; i++) if (meshes[i] != null) Destroy(meshes[i]);
        }
        private static GameObject Child(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        private static Transform RenderObject(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject go = Child(name, parent); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go.transform;
        }
        private Piece CreatePiece(string name, Transform parent, Mesh mesh, Material material, bool outline)
        {
            GameObject go = Child(name, parent);
            RenderObject("Face", go.transform, mesh, material);
            if (outline)
            {
                Transform edge = RenderObject("Ink edge", go.transform, mesh, outlineMaterial);
                edge.localScale = new Vector3(1.10f, .90f, 1.10f); edge.localPosition = new Vector3(0f, -.03f, 0f);
            }
            Bounds bounds = mesh.bounds;
            return new Piece { transform = go.transform, center = bounds.center, extents = bounds.extents * (outline ? 1.10f : 1f),
                restScale = Vector3.one, restRotation = Quaternion.identity };
        }
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        private static float Safe(float value, float fallback, float minimum, float maximum)
            => Finite(value) ? Mathf.Clamp(value, minimum, maximum) : fallback;

        private static Mesh Make(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(start); t.Add(start + 1); t.Add(start + 2); t.Add(start); t.Add(start + 2); t.Add(start + 3);
        }
        private static Vector3 Circle(float angle, float radius, float y) => new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        private static float Radius(int i, int sides, int shape)
        {
            if (shape == 0) return i % 4 < 2 ? 1f : .83f;
            if (shape == 1)
            {
                float angle = i * Mathf.PI * 2f / sides, sector = Mathf.PI / 3f;
                float delta = (angle + sector * .5f) % sector - sector * .5f;
                return .8660254f / Mathf.Cos(delta);
            }
            return 1f;
        }
        private static Mesh RingMesh(string name, int sides, float inner, float thickness, int shape)
        {
            var v = new List<Vector3>(sides * 16); var t = new List<int>(sides * 24);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides; float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 oa = Circle(a, Radius(i, sides, shape), thickness * .5f), ob = Circle(b, Radius(j, sides, shape), thickness * .5f);
                Vector3 ia = Circle(a, inner, thickness * .5f), ib = Circle(b, inner, thickness * .5f);
                Vector3 down = Vector3.up * thickness;
                Quad(v, t, oa, ia, ib, ob); Quad(v, t, ob - down, ib - down, ia - down, oa - down);
                Quad(v, t, oa - down, oa, ob, ob - down); Quad(v, t, ib - down, ib, ia, ia - down);
            }
            return Make(name, v, t);
        }
        private static Mesh BoltMesh()
        {
            var v = new List<Vector3>(160); var t = new List<int>(240);
            Cylinder(v, t, .34f, -.82f, .28f, 8); Cylinder(v, t, .71f, .15f, .50f, 6);
            Cylinder(v, t, .40f, -.48f, -.39f, 8); Cylinder(v, t, .40f, -.18f, -.09f, 8);
            return Make("Scrap bolt", v, t);
        }
        private static void Cylinder(List<Vector3> v, List<int> t, float radius, float bottom, float top, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 aa = Circle(a, radius, bottom), ab = Circle(b, radius, bottom), ba = Circle(a, radius, top), bb = Circle(b, radius, top);
                Quad(v, t, aa, ba, bb, ab);
                int start = v.Count; v.Add(new Vector3(0, top, 0)); v.Add(bb); v.Add(ba); t.Add(start); t.Add(start + 1); t.Add(start + 2);
                start = v.Count; v.Add(new Vector3(0, bottom, 0)); v.Add(aa); v.Add(ab); t.Add(start); t.Add(start + 1); t.Add(start + 2);
            }
        }
        private static Mesh PopMesh()
        {
            var v = new List<Vector3>(36); var t = new List<int>(36);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f, b = (i + 1) * Mathf.PI / 6f;
                int start = v.Count; v.Add(Vector3.zero); v.Add(Circle(b, i % 2 == 0 ? .47f : 1f, 0f)); v.Add(Circle(a, i % 2 == 0 ? 1f : .47f, 0f));
                t.Add(start); t.Add(start + 1); t.Add(start + 2);
            }
            return Make("Comic mechanical pop", v, t);
        }
        private static Mesh PuffMesh()
        {
            var v = new List<Vector3> { Vector3.up, new Vector3(1, 0, 0), Vector3.forward, new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, -1, 0) };
            var t = new List<int> { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
            return Make("Mechanical puff", v, t);
        }
    }
}
