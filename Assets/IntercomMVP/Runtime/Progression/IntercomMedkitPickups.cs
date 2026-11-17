using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    /// <summary>Two reusable test medkits. Collection is proximity based, respects run locks and never wastes full-HP kits.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class IntercomMedkitPickups : MonoBehaviour
    {
        public IntercomMissionDirector mission;
        public Material bodyMaterial, accentMaterial, glowMaterial;
        public Vector3 firstPosition = new Vector3(-4.5f, 0f, -9.5f);
        public Vector3 secondPosition = new Vector3(5f, 0f, 3f);
        [Range(1f, 100f)] public float healing = 35f;
        [Range(.4f, 1.5f)] public float pickupRadius = .85f;
        public int AvailableCount => (kits[0] != null && !kits[0].consumed ? 1 : 0) + (kits[1] != null && !kits[1].consumed ? 1 : 0);
        public int CollectedThisRun { get; private set; }
        public float LastRestored { get; private set; }

        private sealed class Kit
        {
            internal Transform body, ring;
            internal Renderer ringRenderer;
            internal Vector3 floor, collectedFrom, collectedTo;
            internal float age, burst;
            internal bool consumed;
        }
        private readonly Kit[] kits = new Kit[2];
        private readonly RaycastHit[] sightHits = new RaycastHit[8];
        private MaterialPropertyBlock tint;
        private GameObject pool;
        private Mesh ringMesh;
        private AudioSource sound;
        private AudioClip pickupSound;
        private uint observedRun;
        private bool hasRun, focused = true, applicationPaused;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");
        private bool CanPlay => isActiveAndEnabled && focused && Application.isFocused && !applicationPaused && Time.timeScale > 0f
            && mission != null && mission.isActiveAndEnabled && mission.Phase == IntercomMissionPhase.Active
            && mission.playerHealth != null && mission.playerHealth.isActiveAndEnabled && !mission.playerHealth.IsDead
            && mission.playerMotor != null && mission.playerMotor.isActiveAndEnabled
            && (mission.playerVitality == null || !mission.playerVitality.MissionBlocked);

        private void Awake()
        {
            focused = Application.isFocused;
            if(mission == null) mission = GetComponent<IntercomMissionDirector>();
            Prepare();
        }
        private void OnEnable() { focused = Application.isFocused; applicationPaused = false; }
        private void Prepare()
        {
            if(pool != null || bodyMaterial == null || accentMaterial == null || glowMaterial == null) return;
            // Native graphics objects must be created in the main-thread lifecycle,
            // never in a MonoBehaviour field initializer during scene deserialization.
            if(tint == null) tint = new MaterialPropertyBlock();
            hasRun = false;
            pool = new GameObject("Reusable medkits - two test pickups"); pool.transform.SetParent(transform, true);
            ringMesh = BuildRing();
            sound = pool.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.loop = false;
            sound.spatialBlend = 0f; sound.volume = .34f;
            pickupSound = BuildPickupSound();
            for(int i = 0; i < kits.Length; i++)
            {
                var body = new GameObject("Medkit " + (i + 1)); body.transform.SetParent(pool.transform, true);
                // Low cream case with a dark seam, handle and a teal plus on its top: readable from the game camera.
                Part(body.transform, "Cream medical case", Vector3.zero, new Vector3(.66f,.27f,.46f), bodyMaterial, new Color(.93f,.88f,.72f));
                Part(body.transform, "Teal seam", new Vector3(0f,.01f,0f), new Vector3(.675f,.055f,.475f), accentMaterial, new Color(.08f,.40f,.36f));
                Part(body.transform, "Top lid", new Vector3(0f,.14f,0f), new Vector3(.59f,.045f,.40f), bodyMaterial, new Color(1f,.96f,.84f));
                Part(body.transform, "Medical plus horizontal", new Vector3(0f,.168f,0f), new Vector3(.30f,.025f,.09f), accentMaterial, new Color(.13f,.63f,.46f));
                Part(body.transform, "Medical plus vertical", new Vector3(0f,.169f,0f), new Vector3(.09f,.025f,.30f), accentMaterial, new Color(.13f,.63f,.46f));
                Part(body.transform, "Handle left", new Vector3(-.10f,.20f,.11f), new Vector3(.04f,.12f,.04f), accentMaterial, new Color(.08f,.40f,.36f));
                Part(body.transform, "Handle right", new Vector3(.10f,.20f,.11f), new Vector3(.04f,.12f,.04f), accentMaterial, new Color(.08f,.40f,.36f));
                Part(body.transform, "Handle grip", new Vector3(0f,.25f,.11f), new Vector3(.24f,.04f,.04f), accentMaterial, new Color(.08f,.40f,.36f));
                var ring = new GameObject("Medkit ground ring " + (i + 1)); ring.transform.SetParent(pool.transform, true);
                ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                var renderer = ring.AddComponent<MeshRenderer>(); renderer.sharedMaterial = glowMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                kits[i] = new Kit { body = body.transform, ring = ring.transform, ringRenderer = renderer };
                body.SetActive(false); ring.SetActive(false);
            }
        }
        private void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = size;
            Collider collider = part.GetComponent<Collider>(); if(collider != null) { collider.enabled = false; Destroy(collider); }
            Renderer renderer = part.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            tint.SetColor(BaseColorId, color); tint.SetColor(ColorId, color); renderer.SetPropertyBlock(tint);
        }
        private void ObserveRun()
        {
            if(mission == null || (hasRun && observedRun == mission.RunRevision)) return;
            hasRun = true; observedRun = mission.RunRevision; CollectedThisRun = 0; LastRestored = 0f;
            for(int i = 0; i < kits.Length; i++)
            {
                if(kits[i] == null) continue;
                Kit kit = kits[i]; kit.floor = i == 0 ? firstPosition : secondPosition;
                kit.consumed = false; kit.age = i * .65f; kit.burst = 0f;
            }
            if(sound != null) sound.Stop();
        }
        private void Update()
        {
            if(pool == null) Prepare();
            ObserveRun();
            bool active = CanPlay;
            if(!active)
            {
                // Pause freezes the existing pose; menus/results hide the world pickup presentation entirely.
                if(sound != null && sound.isPlaying) sound.Stop();
                if(mission == null || !mission.isActiveAndEnabled || mission.Phase != IntercomMissionPhase.Active) Hide();
                return;
            }
            float dt = Time.deltaTime;
            if(!Finite(dt) || dt <= 0f) return;
            dt = Mathf.Min(dt,.1f);
            for(int i = 0; i < kits.Length; i++)
            {
                Kit kit = kits[i]; if(kit == null) continue;
                kit.age += dt;
                if(!kit.consumed) TryCollect(i);
                if(kit.consumed)
                {
                    if(kit.burst <= 0f) { kit.body.gameObject.SetActive(false); kit.ring.gameObject.SetActive(false); continue; }
                    kit.burst = Mathf.Max(0f,kit.burst-dt);
                    float t = 1f-kit.burst/.5f;
                    kit.body.position = kit.collectedFrom + (kit.collectedTo-kit.collectedFrom)*Mathf.Clamp01(t*2f);
                    kit.body.localScale = Vector3.one*Mathf.Max(0f,1f-t*2f);
                    kit.body.gameObject.SetActive(t < .5f);
                    PresentRing(kit,1f+t*2.5f,(1f-t)*.75f);
                }
                else
                {
                    kit.body.gameObject.SetActive(true); kit.ring.gameObject.SetActive(true);
                    kit.body.SetPositionAndRotation(kit.floor + Vector3.up*(.20f+Mathf.Sin(kit.age*2.6f)*.035f),Quaternion.Euler(0f,kit.age*24f,0f));
                    kit.body.localScale = Vector3.one;
                    PresentRing(kit,1f+Mathf.Sin(kit.age*2.6f)*.045f,.38f+Mathf.Sin(kit.age*2.6f)*.08f);
                }
            }
        }
        /// <summary>Returns true only for an actual heal; full health leaves the medkit available.</summary>
        public bool TryCollect(int index)
        {
            ObserveRun();
            if(!CanPlay || index < 0 || index >= kits.Length || kits[index] == null) return false;
            Kit kit = kits[index]; if(kit.consumed) return false;
            Vector3 hero = mission.playerMotor.transform.position, delta = hero-kit.floor;
            float radius = Finite(pickupRadius) ? Mathf.Clamp(pickupRadius,.4f,1.5f) : .85f;
            if(!Finite(delta.x) || !Finite(delta.y) || !Finite(delta.z) || Mathf.Abs(delta.y) > .8f
                || delta.x*delta.x+delta.z*delta.z > radius*radius || !HasSight(hero,kit.floor)) return false;
            float amount = Finite(healing) ? Mathf.Clamp(healing,1f,100f) : 35f;
            IntercomHealth health = mission.playerHealth;
            if(health.Current >= health.Maximum) return false;
            float restored = Mathf.Min(amount,health.Maximum-health.Current);
            // Claim before dispatching Heal callbacks, preventing recursive overlap from consuming twice.
            kit.consumed = true;
            if(!health.Heal(amount)) { kit.consumed = false; return false; }
            LastRestored = restored; CollectedThisRun++;
            kit.burst = .5f; kit.collectedFrom = kit.body.position; kit.collectedTo = hero+Vector3.up*.85f;
            if(sound != null && pickupSound != null) sound.PlayOneShot(pickupSound);
            return true;
        }
        private bool HasSight(Vector3 hero, Vector3 floor)
        {
            Vector3 origin = hero+Vector3.up*.45f, delta = floor+Vector3.up*.3f-origin;
            float distance = delta.magnitude; if(distance <= .001f) return true;
            int count = Physics.RaycastNonAlloc(origin,delta/distance,sightHits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count >= sightHits.Length) return false;
            for(int i = 0; i < count; i++)
            {
                Collider hit = sightHits[i].collider;
                if(hit == null || hit is CharacterController || hit.isTrigger || hit.transform.IsChildOf(mission.playerHealth.transform)
                    || (pool != null && hit.transform.IsChildOf(pool.transform))) continue;
                return false;
            }
            return true;
        }
        private void PresentRing(Kit kit, float size, float opacity)
        {
            kit.ring.gameObject.SetActive(true); kit.ring.position = kit.floor+Vector3.up*.035f;
            kit.ring.localScale = Vector3.one*size;
            Color color = new Color(.24f,.91f,.61f,opacity);
            tint.SetColor(BaseColorId,color); tint.SetColor(ColorId,color); kit.ringRenderer.SetPropertyBlock(tint);
        }
        private void Hide()
        { foreach(Kit kit in kits) if(kit != null) { kit.body.gameObject.SetActive(false); kit.ring.gameObject.SetActive(false); } }
        private void OnApplicationFocus(bool value) { focused = value; if(!value && sound != null) sound.Stop(); }
        private void OnApplicationPause(bool value) { applicationPaused = value; if(value && sound != null) sound.Stop(); }
        private void OnDisable() { Hide(); if(sound != null) sound.Stop(); }
        private void OnDestroy()
        { if(pool != null) Destroy(pool); if(ringMesh != null) Destroy(ringMesh); if(pickupSound != null) Destroy(pickupSound); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Mesh BuildRing()
        {
            const int sides = 48; var vertices = new List<Vector3>(sides*4); var triangles = new List<int>(sides*6);
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2f/sides,b=(i+1)*Mathf.PI*2f/sides; int start=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*.43f,0f,Mathf.Sin(a)*.43f)); vertices.Add(new Vector3(Mathf.Cos(a)*.49f,0f,Mathf.Sin(a)*.49f));
                vertices.Add(new Vector3(Mathf.Cos(b)*.49f,0f,Mathf.Sin(b)*.49f)); vertices.Add(new Vector3(Mathf.Cos(b)*.43f,0f,Mathf.Sin(b)*.43f));
                triangles.Add(start);triangles.Add(start+2);triangles.Add(start+1);triangles.Add(start);triangles.Add(start+3);triangles.Add(start+2);
            }
            var mesh = new Mesh { name="Medkit ground ring" }; mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static AudioClip BuildPickupSound()
        {
            const int rate=22050,length=6174; var data=new float[length];
            for(int i=0;i<length;i++)
            {
                float t=i/(float)rate, envelope=Mathf.Min(1f,t/.012f)*(1f-i/(float)length);
                // Integrated phase keeps the two-note transition continuous, avoiding an audible click.
                float cycles=t<.09f ? 659.25f*t : 659.25f*.09f+987.77f*(t-.09f);
                data[i]=(Mathf.Sin(2f*Mathf.PI*cycles)*.18f+Mathf.Sin(4f*Mathf.PI*cycles)*.045f)*envelope*envelope;
            }
            AudioClip clip=AudioClip.Create("Medkit restore chime",length,1,rate,false);clip.SetData(data,0);return clip;
        }
    }
}
