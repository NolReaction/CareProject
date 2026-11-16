using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    /// <summary>Warm pooled currency: scatter, bounce, hover, attract and collect. No Rigidbody or per-drop allocations.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class IntercomGearPickups : MonoBehaviour
    {
        public IntercomMissionDirector mission;
        public Material gearMaterial;
        [Range(.5f, 4f)] public float magnetRadius = 2.2f;
        [Range(.2f, 1f)] public float pickupRadius = .55f;
        public int CollectedGears => ledger.CollectedGears;
        public int ActiveCoins => ledger.ActiveCount;
        public int UncollectedGears => ledger.ActiveValue;
        public int LastPickupValue { get; private set; }
        public float PickupPulse { get; private set; }
        public int TotalPickups { get; private set; }

        private sealed class Coin
        {
            internal Transform transform;
            internal Vector3 position, velocity;
            internal float floor, age, angle;
            internal bool attracted, sweeping;
        }
        private readonly IntercomGearCollectionState ledger = new IntercomGearCollectionState();
        private readonly Coin[] coins = new Coin[IntercomGearCollectionState.Capacity];
        private readonly HashSet<int> rewardedProps = new HashSet<int>();
        private GameObject poolRoot;
        private Mesh mesh;
        private Material fallbackMaterial;
        private int prepared;
        private uint observedRun, random;
        private bool focused = true, applicationPaused;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color"), EmissionId = Shader.PropertyToID("_EmissionColor");
        private bool ActiveRun => mission != null && mission.Phase == IntercomMissionPhase.Active && mission.Mode != IntercomMissionMode.FreePlay;

        private void Awake() { focused = Application.isFocused; if (mission == null) mission = GetComponent<IntercomMissionDirector>(); Prepare(); }
        private void OnEnable() { focused = Application.isFocused; applicationPaused = false; }
        private void Prepare()
        {
            if (poolRoot != null) return;
            if (mission == null) return;
            if (gearMaterial == null && mission.deathEffects != null) gearMaterial = mission.deathEffects.gearMaterial;
            if (gearMaterial == null && mission.enemyDefinition != null) gearMaterial = mission.enemyDefinition.detailMaterial;
            if (gearMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) return;
                fallbackMaterial = new Material(shader); gearMaterial = fallbackMaterial;
            }
            mesh = BuildGearMesh();
            poolRoot = new GameObject("Collectible gear pool - 96 reusable coins");
            poolRoot.transform.SetParent(transform, true);
            var tint = new MaterialPropertyBlock();
            tint.SetColor(BaseColorId, new Color(1f, .67f, .12f, 1f));
            tint.SetColor(ColorId, new Color(1f, .67f, .12f, 1f));
            tint.SetColor(EmissionId, new Color(.22f, .10f, .01f, 1f));
            // Created before combat; subsequent drops only reset transforms and ledger slots.
            for (int i = 0; i < coins.Length; i++)
            {
                var root = new GameObject("Gear coin " + i); root.transform.SetParent(poolRoot.transform, true);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = gearMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.SetPropertyBlock(tint); root.SetActive(false);
                coins[i] = new Coin { transform = root.transform }; prepared++;
            }
        }
        private void ObserveRun()
        {
            if (mission == null || observedRun == mission.RunRevision) return;
            ClearForTransition(); ledger.BeginRun(); rewardedProps.Clear(); TotalPickups = 0;
            LastPickupValue = 0; PickupPulse = 0f; observedRun = mission.RunRevision;
        }
        private void Update()
        {
            ObserveRun();
            if (!focused || applicationPaused || Time.timeScale <= 0f) return;
            if (poolRoot == null) Prepare();
            float dt = Time.deltaTime;
            if (!Finite(dt) || dt <= 0f) return;
            PickupPulse = Mathf.Max(0f, PickupPulse - dt * 3.4f);
            if (mission == null || mission.playerMotor == null) return;
            bool sweep = mission.Phase == IntercomMissionPhase.Victory;
            if (!ActiveRun && !sweep) return;
            Vector3 target = mission.playerMotor.transform.position + Vector3.up * .62f;
            for (int i = 0; i < prepared; i++)
            {
                Coin c = coins[i];
                if (!c.transform.gameObject.activeSelf) continue;
                c.age += dt; c.angle += dt * (c.attracted ? 650f : 145f);
                if (ledger.ValueAt(i) <= 0)
                {
                    // Victory settlement has already consumed the token; keep its fly-in visible for a short finish.
                    if (!c.sweeping) { c.transform.gameObject.SetActive(false); continue; }
                    Fly(c, target, dt);
                    if ((c.position - target).sqrMagnitude < .10f) c.transform.gameObject.SetActive(false);
                    Present(c, true); continue;
                }
                Vector3 flat = target - c.position; flat.y = 0f;
                if (c.age >= .32f && flat.sqrMagnitude <= Safe(magnetRadius, 2.2f, .5f, 4f) * Safe(magnetRadius, 2.2f, .5f, 4f)) c.attracted = true;
                if (c.attracted)
                {
                    Fly(c, target, dt);
                    if ((c.position - target).sqrMagnitude <= Safe(pickupRadius, .55f, .2f, 1f) * Safe(pickupRadius, .55f, .2f, 1f))
                    { Collect(i); c.transform.gameObject.SetActive(false); continue; }
                }
                else if (c.age < 1.25f)
                {
                    float step = Mathf.Min(dt, .05f);
                    c.velocity.y -= 9.8f * step; c.position += c.velocity * step;
                    if (c.position.y < c.floor)
                    {
                        c.position.y = c.floor; c.velocity.y = Mathf.Abs(c.velocity.y) * .33f;
                        c.velocity.x *= .58f; c.velocity.z *= .58f;
                    }
                }
                else c.position.y = c.floor + .085f + Mathf.Sin(c.age * 3.7f + i) * .045f;
                Present(c, c.attracted);
            }
        }
        private static void Fly(Coin c, Vector3 target, float dt)
        {
            float speed = 7f + Mathf.Min(12f, c.age * 4f);
            c.position = Vector3.MoveTowards(c.position, target, speed * Mathf.Min(dt, .05f));
        }
        private static void Present(Coin c, bool attracting)
        {
            c.transform.SetPositionAndRotation(c.position, Quaternion.Euler(90f + Mathf.Sin(c.age * 3f) * 9f, c.angle, 0f));
            float pop = Mathf.Clamp01(c.age / .12f);
            c.transform.localScale = Vector3.one * (.18f * pop * (attracting ? .92f : 1f));
        }
        private void Collect(int slot)
        {
            if (!ledger.TryCollect(slot, out int value)) return;
            LastPickupValue = value; PickupPulse = 1f; TotalPickups++;
        }
        public void DropRobot(Vector3 position, IntercomNpcArchetype archetype, uint lifeSeed)
        {
            ObserveRun();
            // RecordKill has already switched the final enemy into Victory; its coins still belong to this run.
            if (mission == null || mission.Mode == IntercomMissionMode.FreePlay
                || (mission.Phase != IntercomMissionPhase.Active && mission.Phase != IntercomMissionPhase.Victory)) return;
            int count = archetype == IntercomNpcArchetype.Heavy ? 4 : archetype == IntercomNpcArchetype.Runner ? 2 : 3;
            Drop(position, count, archetype == IntercomNpcArchetype.Heavy ? 2 : 1, lifeSeed);
        }
        public void DropProp(Vector3 position, int sourceId, IntercomPropKind kind)
        {
            ObserveRun();
            if (!ActiveRun || !rewardedProps.Add(sourceId)) return;
            int count = kind == IntercomPropKind.Crate ? 4 : kind == IntercomPropKind.Relay ? 2 : 1;
            Drop(position, count, 1, unchecked((uint)sourceId * 747796405u));
        }
        private void Drop(Vector3 position, int count, int value, uint seed)
        {
            if (!isActiveAndEnabled || prepared == 0 || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return;
            random = seed == 0 ? 0x51eaf73du : seed;
            for (int i = 0; i < count; i++)
            {
                if (!ledger.TryDrop(value, out int slot, out bool activated)) break;
                if (!activated) continue;
                Coin c = coins[slot]; float a = (i + Next() * .3f) * Mathf.PI * 2f / count;
                c.position = position + Vector3.up * .58f;
                c.velocity = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (.7f + Next() * .8f) + Vector3.up * (2.5f + Next() * .7f);
                c.floor = position.y + .17f; c.age = 0f; c.angle = Next() * 360f; c.attracted = c.sweeping = false;
                c.transform.gameObject.SetActive(true); Present(c, false);
            }
        }
        /// <summary>A cleared arena draws remaining coins to the hero; a defeat never awards missed pickups.</summary>
        public void SweepForVictory()
        {
            ObserveRun();
            if (mission == null || mission.Phase != IntercomMissionPhase.Victory || mission.Mode == IntercomMissionMode.FreePlay) return;
            for (int i = 0; i < prepared; i++)
                if (ledger.ValueAt(i) > 0) { coins[i].attracted = coins[i].sweeping = true; Collect(i); }
        }
        public void ClearForTransition()
        {
            ledger.ClearUncollected();
            for (int i = 0; i < prepared; i++) if (coins[i] != null) coins[i].transform.gameObject.SetActive(false);
        }
        private void OnApplicationFocus(bool value) { focused = value; }
        private void OnApplicationPause(bool value) { applicationPaused = value; }
        private void OnDisable() { ClearForTransition(); }
        private void OnDestroy()
        {
            if (poolRoot != null) Destroy(poolRoot);
            if (mesh != null) Destroy(mesh);
            if (fallbackMaterial != null) Destroy(fallbackMaterial);
        }
        private float Next() { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (random & 0xffffffu) / 16777216f; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Safe(float value, float fallback, float min, float max) => Finite(value) ? Mathf.Clamp(value, min, max) : fallback;
        private static Mesh BuildGearMesh()
        {
            const int sides = 40;
            var vertices = new List<Vector3>(sides * 16); var triangles = new List<int>(sides * 24);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                float ra = i % 4 == 0 || i % 4 == 3 ? .76f : 1f;
                int next = (i + 1) % sides; float rb = next % 4 == 0 || next % 4 == 3 ? .76f : 1f;
                Vector3 outerA = Ring(a, ra, .12f), outerB = Ring(b, rb, .12f);
                Vector3 innerA = Ring(a, .32f, .12f), innerB = Ring(b, .32f, .12f), down = Vector3.up * .24f;
                Quad(vertices, triangles, outerA, innerA, innerB, outerB);
                Quad(vertices, triangles, outerB - down, innerB - down, innerA - down, outerA - down);
                Quad(vertices, triangles, outerA - down, outerA, outerB, outerB - down);
                Quad(vertices, triangles, innerB - down, innerB, innerA, innerA - down);
            }
            var result = new Mesh { name = "Collectible ten tooth gear" }; result.SetVertices(vertices); result.SetTriangles(triangles, 0);
            result.RecalculateNormals(); result.RecalculateBounds(); return result;
        }
        private static Vector3 Ring(float angle, float radius, float y) => new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
    }
}
