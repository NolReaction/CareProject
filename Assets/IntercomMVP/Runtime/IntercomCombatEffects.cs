using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    /// <summary>Bounded cosmetic effects. Shells use short swept casts, never dynamic rigidbodies.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(250)]
    public sealed class IntercomCombatEffects : MonoBehaviour
    {
        public IntercomSimpleShooter shooter;
        public Material flashMaterial;
        public Material casingMaterial;
        public Material impactMaterial;
        [Range(16, 96)] public int shellPoolSize = 48;
        [Range(16, 128)] public int sparkPoolSize = 64;
        [Min(0.2f)] public float shellLifetime = 2.5f;
        public LayerMask surfaceMask = ~0;

        private struct Piece
        {
            public Transform visual;
            public Vector3 velocity, spin, size;
            public float remaining, duration;
            public byte kind; // 0 shell, 1 muzzle star, 2 impact spark
            public int bounces;
            public int bornFrame;
        }
        private Piece[] shells, sparks;
        private int nextShell, nextSpark;
        private GameObject root;
        private Mesh shellMesh, sparkMesh;
        private IntercomSimpleShooter subscribed;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private bool focused = true, paused;

        private void Awake()
        {
            if (shooter == null) shooter = GetComponent<IntercomSimpleShooter>();
            if (flashMaterial == null || casingMaterial == null || impactMaterial == null)
            {
                Debug.LogWarning("Intercom MVP: подключи материалы эффектов через Setup Combat Feedback.", this);
                enabled = false; return;
            }
            root = new GameObject("Intercom Combat Effects Pool");
            root.AddComponent<IntercomOccluder>().hideWhenBlocking = false;
            shellMesh = MakeCasingMesh();
            sparkMesh = MakeStar();
            shells = CreatePool(Mathf.Clamp(shellPoolSize, 16, 96), shellMesh, casingMaterial, "Casing");
            sparks = CreatePool(Mathf.Clamp(sparkPoolSize, 16, 128), sparkMesh, impactMaterial, "Spark");
        }

        private void OnEnable() { Subscribe(); }
        private void Subscribe()
        {
            if (subscribed == shooter) return;
            Unsubscribe(); subscribed = shooter;
            if (subscribed == null) return;
            subscribed.BarrelFired += Barrel; subscribed.ProjectileHit += Impact;
        }
        private void Unsubscribe()
        {
            if (subscribed != null) { subscribed.BarrelFired -= Barrel; subscribed.ProjectileHit -= Impact; }
            subscribed = null;
        }
        private bool CanEmit => isActiveAndEnabled && focused && !paused && Time.timeScale > 0f && root != null;

        private void Barrel(Transform muzzle, bool rightHand)
        {
            if (!CanEmit || muzzle == null) return;
            ref Piece flash = ref sparks[nextSpark]; nextSpark = (nextSpark + 1) % sparks.Length;
            Activate(ref flash, muzzle.position + muzzle.forward * 0.055f, muzzle.rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)),
                new Vector3(0.24f, 0.24f, 0.32f), 0.065f, 1, flashMaterial);
            flash.velocity = Vector3.zero;
            ref Piece shell = ref shells[nextShell]; nextShell = (nextShell + 1) % shells.Length;
            // This is a visual ejection point near the chamber; marker itself remains the barrel tip.
            Vector3 outwards = muzzle.right * (rightHand ? 1f : -1f);
            Activate(ref shell, muzzle.position - muzzle.forward * 0.14f + Vector3.up * 0.035f,
                Random.rotation, new Vector3(0.045f, 0.10f, 0.045f), Mathf.Max(0.2f, shellLifetime), 0, casingMaterial);
            shell.velocity = outwards * Random.Range(0.8f, 1.4f) + Vector3.up * Random.Range(1.5f, 2.1f) - muzzle.forward * 0.35f;
            shell.spin = new Vector3(Random.Range(-420f, 420f), Random.Range(220f, 650f), Random.Range(-420f, 420f));
        }

        private void Impact(Vector3 point, Vector3 normal, bool target)
        {
            if (!CanEmit) return;
            Quaternion facing = Quaternion.FromToRotation(Vector3.forward, normal);
            for (int i = 0; i < 6; i++)
            {
                ref Piece spark = ref sparks[nextSpark]; nextSpark = (nextSpark + 1) % sparks.Length;
                Vector3 radial = Quaternion.AngleAxis(i * 60f + Random.Range(-12f, 12f), normal)
                    * (facing * Vector3.up);
                Activate(ref spark, point + normal * 0.035f, Quaternion.LookRotation(normal, radial),
                    new Vector3(0.045f, target ? 0.20f : 0.13f, 0.045f), Random.Range(0.15f, 0.24f), 2,
                    target ? flashMaterial : impactMaterial);
                spark.velocity = radial * Random.Range(1.1f, 1.8f) + normal * 0.5f;
            }
        }

        private void LateUpdate()
        {
            Subscribe();
            if (root == null || !focused || paused || Time.deltaTime <= 0f) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < shells.Length; i++) Tick(ref shells[i], dt);
            for (int i = 0; i < sparks.Length; i++) Tick(ref sparks[i], dt);
        }

        private void Tick(ref Piece piece, float dt)
        {
            if (piece.remaining <= 0f || piece.bornFrame == Time.frameCount) return;
            piece.remaining -= dt;
            if (piece.remaining <= 0f) { piece.visual.gameObject.SetActive(false); return; }
            if (piece.kind == 0)
            {
                if (piece.bounces < 3)
                {
                    piece.velocity += Vector3.down * (10f * dt);
                    Vector3 delta = piece.velocity * dt;
                    float travel = delta.magnitude;
                    if (travel > 0.00001f && CastSurface(piece.visual.position, delta / travel, travel + 0.025f, out RaycastHit hit))
                    {
                        piece.visual.position = hit.point + hit.normal * 0.025f;
                        piece.velocity = Vector3.Reflect(piece.velocity, hit.normal) * 0.38f;
                        piece.spin *= 0.45f;
                        piece.bounces++;
                        if (piece.bounces >= 3) { piece.velocity = Vector3.zero; piece.spin = Vector3.zero; }
                    }
                    else piece.visual.position += delta;
                    piece.visual.Rotate(piece.spin * dt, Space.World);
                }
                float fade = Mathf.Clamp01(piece.remaining / 0.35f);
                piece.visual.localScale = piece.size * fade;
            }
            else
            {
                piece.visual.position += piece.velocity * dt;
                float fraction = piece.remaining / piece.duration;
                piece.visual.localScale = piece.size * (piece.kind == 1 ? fraction : Mathf.Sqrt(fraction));
            }
        }

        private bool CastSurface(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, surfaceMask, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Transform item = hits[i].transform;
                Transform owner = shooter != null && shooter.ownerRoot != null ? shooter.ownerRoot : transform;
                if (item == owner || item.IsChildOf(owner)) continue;
                if (hits[i].distance >= closest) continue;
                closest = hits[i].distance; nearest = hits[i]; found = true;
            }
            return found;
        }

        private Piece[] CreatePool(int count, Mesh mesh, Material material, string label)
        {
            Piece[] pool = new Piece[count];
            for (int i = 0; i < count; i++)
            {
                GameObject item = new GameObject(label + " " + i);
                item.transform.SetParent(root.transform, false);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = item.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                item.SetActive(false); pool[i].visual = item.transform;
            }
            return pool;
        }

        private static void Activate(ref Piece piece, Vector3 position, Quaternion rotation, Vector3 size, float duration, byte kind, Material material)
        {
            piece.visual.SetPositionAndRotation(position, rotation); piece.visual.localScale = size;
            piece.visual.GetComponent<Renderer>().sharedMaterial = material;
            piece.size = size; piece.duration = piece.remaining = duration; piece.kind = kind;
            piece.bounces = 0; piece.bornFrame = Time.frameCount; piece.spin = Vector3.zero; piece.visual.gameObject.SetActive(true);
        }
        private static Mesh MakeCasingMesh()
        {
            // Eight sides, a wider extraction rim and a bevel at the open mouth.
            const int sides = 8;
            float[] heights = { -0.5f, -0.39f, -0.33f, 0.38f, 0.5f };
            float[] radii = { 0.48f, 0.48f, 0.36f, 0.36f, 0.30f };
            Vector3[] vertices = new Vector3[sides * 5];
            int[] triangles = new int[sides * 4 * 6 + (sides - 2) * 6];
            for (int ring = 0; ring < 5; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    vertices[ring * sides + i] = new Vector3(Mathf.Cos(angle) * radii[ring], heights[ring], Mathf.Sin(angle) * radii[ring]);
                }
            int n = 0;
            for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < sides; i++)
                {
                    int a = ring * sides + i, b = ring * sides + (i + 1) % sides;
                    int c = a + sides, d = b + sides;
                    triangles[n++] = a; triangles[n++] = c; triangles[n++] = b;
                    triangles[n++] = b; triangles[n++] = c; triangles[n++] = d;
                }
            for (int i = 1; i < sides - 1; i++)
            {
                triangles[n++] = 0; triangles[n++] = i; triangles[n++] = i + 1;
                triangles[n++] = 32; triangles[n++] = 32 + i + 1; triangles[n++] = 32 + i;
            }
            Mesh mesh = new Mesh { name = "Intercom Bevelled Brass Casing", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.UploadMeshData(true); return mesh;
        }
        private static Mesh MakeStar()
        {
            const int points = 12;
            Vector3[] vertices = new Vector3[points + 2];
            int[] triangles = new int[points * 6];
            vertices[0] = Vector3.forward * 0.5f; vertices[points + 1] = Vector3.back * 0.35f;
            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2f / points, radius = i % 2 == 0 ? 0.5f : 0.17f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                int j = (i + 1) % points + 1;
                triangles[i*6] = 0; triangles[i*6+1] = i+1; triangles[i*6+2] = j;
                triangles[i*6+3] = points+1; triangles[i*6+4] = j; triangles[i*6+5] = i+1;
            }
            Mesh mesh = new Mesh { name = "Intercom Cartoon Star", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.UploadMeshData(true); return mesh;
        }
        private void Clear()
        {
            if (shells != null) for (int i = 0; i < shells.Length; i++) { shells[i].remaining = 0f; shells[i].visual.gameObject.SetActive(false); }
            if (sparks != null) for (int i = 0; i < sparks.Length; i++) { sparks[i].remaining = 0f; sparks[i].visual.gameObject.SetActive(false); }
        }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) Clear(); }
        private void OnApplicationPause(bool value) { paused = value; if (value) Clear(); }
        private void OnDisable() { Unsubscribe(); Clear(); }
        private void OnDestroy()
        {
            Unsubscribe(); if (root != null) Destroy(root);
            if (shellMesh != null) Destroy(shellMesh); if (sparkMesh != null) Destroy(sparkMesh);
        }
    }
}
