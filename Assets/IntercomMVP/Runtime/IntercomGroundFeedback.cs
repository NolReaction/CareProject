using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    /// <summary>Soft boot contacts and small additive torso accents. Legs and collision geometry stay authored.</summary>
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class IntercomGroundFeedback : MonoBehaviour
    {
        public IntercomPlayerMotor motor;
        public IntercomPlayerAudio audioSource;
        public IntercomSimpleShooter shooter;
        public Animator animator;
        public Transform leftFoot;
        public Transform rightFoot;
        public Transform torso;
        [Tooltip("Included radial material. Its shader is retained by the build through this reference.")]
        public Material contactMaterial;
        [Tooltip("Sole centres in the corresponding foot bone's local space.")]
        public Vector3 leftSoleOffset;
        public Vector3 rightSoleOffset;
        public bool soleOffsetsCalibrated;
        public LayerMask groundMask = ~0;
        [Range(0f, 0.6f)] public float shadowOpacity = 0.32f;
        public Vector2 shadowSize = new Vector2(0.34f, 0.46f);
        [Min(0.05f)] public float shadowFadeHeight = 0.32f;
        [Min(0.001f)] public float surfaceOffset = 0.009f;
        [Range(0f, 5f)] public float runLeanDegrees = 2.5f;
        [Range(0f, 4f)] public float runSwayDegrees = 1.25f;
        [Range(0f, 8f)] public float recoilDegrees = 4f;
        [Range(0f, 0.025f)] public float contactCompression = 0.008f;
        public Color dustColor = new Color(0.76f, 0.68f, 0.47f, 0.26f);

        private struct Contact
        {
            internal bool valid;
            internal Vector3 point, normal;
            internal float height;
        }
        private struct Puff
        {
            internal Transform transform;
            internal MeshRenderer renderer;
            internal Vector3 origin, drift;
            internal float age;
            internal int bornFrame;
            internal bool active;
        }
        private const int DustCapacity = 12;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private readonly IntercomFootstepClock fallbackClock = new IntercomFootstepClock();
        private MaterialPropertyBlock properties;
        private IntercomPlayerAudio subscribedAudio;
        private IntercomSimpleShooter subscribedShooter;
        private GameObject effectsRoot;
        private Mesh discMesh;
        private Transform[] shadows;
        private MeshRenderer[] shadowRenderers;
        private Puff[] dust;
        private Contact leftContact, rightContact;
        private int nextPuff;
        private bool nextLeft = true;
        private bool pendingContact;
        private float compression, recoil;
        private bool poseApplied;
        private Transform appliedTorso;
        private Vector3 savedLocalPosition;
        private Quaternion savedLocalRotation;
        private bool focused = true;
        private bool paused;

        private bool Active => isActiveAndEnabled && focused && !paused && Time.deltaTime > 0f;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            if (motor == null) motor = GetComponent<IntercomPlayerMotor>();
            if (audioSource == null) audioSource = GetComponent<IntercomPlayerAudio>();
            if (shooter == null) shooter = GetComponent<IntercomSimpleShooter>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
                {
                    if (leftFoot == null && bone.name == "L_Foot") leftFoot = bone;
                    if (rightFoot == null && bone.name == "R_Foot") rightFoot = bone;
                    if (torso == null && bone.name == "Torso") torso = bone;
                }
                // Once at startup for old scenes without editor-calibrated offsets.
                if (!soleOffsetsCalibrated) CalibrateSoles();
            }
            if (contactMaterial != null) CreateEffects();
        }

        private void OnEnable() { Subscribe(); }
        private void Update()
        {
            // Restore the exact previous Animator pose before its next evaluation.
            // Never multiply an inverse onto an already modified bone each frame.
            RestorePose();
            Subscribe();
            if (!Active) ClearTransientState();
        }

        private void LateUpdate()
        {
            if (!Active || motor == null || !motor.isActiveAndEnabled)
            {
                RestorePose();
                ClearTransientState();
                HideEffects();
                return;
            }
            float dt = Time.deltaTime;
            bool grounded = motor.IsGrounded;
            leftContact = Probe(leftFoot, leftSoleOffset);
            rightContact = Probe(rightFoot, rightSoleOffset);
            if (shadows != null)
            {
                UpdateShadow(0, leftContact, grounded);
                UpdateShadow(1, rightContact, grounded);
            }
            bool moving = grounded && motor.ActualPlanarSpeed > 0.08f;
            bool animated = animator != null && animator.isActiveAndEnabled
                && animator.runtimeAnimatorController != null && animator.speed > 0f;
            moving = moving && animated;
            float phase = animated ? animator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f;
            if (!animated) fallbackClock.Reset();
            // The audio contact event is authoritative when available. This
            // fallback keeps visual contacts working on a character without audio.
            if (audioSource == null || !audioSource.isActiveAndEnabled || audioSource.profile == null)
            {
                if (animated && fallbackClock.Tick(phase, animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0f, moving))
                    pendingContact = true;
            }
            else fallbackClock.Reset();
            if (pendingContact && moving)
            {
                compression = 1f;
                Contact contact = ChooseContact();
                if (contact.valid && contact.height <= 0.065f) EmitDust(contact);
            }
            pendingContact = false;
            UpdateDust(dt);
            if (animated) ApplyPose(phase, moving);
            compression = Mathf.MoveTowards(compression, 0f, dt * 13f);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 12f);
        }

        private Contact ChooseContact()
        {
            Contact contact;
            if (!leftContact.valid) contact = rightContact;
            else if (!rightContact.valid) contact = leftContact;
            else if (Mathf.Abs(leftContact.height - rightContact.height) > 0.025f)
                contact = leftContact.height < rightContact.height ? leftContact : rightContact;
            else contact = nextLeft ? leftContact : rightContact;
            nextLeft = !nextLeft;
            return contact;
        }

        private void ApplyPose(float phase, bool moving)
        {
            if (torso == null) return;
            RestorePose();
            appliedTorso = torso;
            savedLocalPosition = torso.localPosition;
            savedLocalRotation = torso.localRotation;
            poseApplied = true;
            Vector3 forward = motor.FacingDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float weight = moving ? Mathf.Clamp01(motor.ActualPlanarSpeed / Mathf.Max(0.1f, motor.moveSpeed)) : 0f;
            float side = moving ? animator.GetFloat("MoveX") : 0f;
            float travel = moving ? animator.GetFloat("MoveY") : 0f;
            float sway = Mathf.Sin((phase % 1f) * Mathf.PI * 2f) * runSwayDegrees * weight;
            float lean = travel * runLeanDegrees * weight - recoil * recoilDegrees;
            torso.rotation = Quaternion.AngleAxis(lean, right)
                * Quaternion.AngleAxis(sway - side * runLeanDegrees * weight, forward) * torso.rotation;
            torso.position -= Vector3.up * (compression * contactCompression * weight);
        }

        private void RestorePose()
        {
            if (!poseApplied) return;
            if (appliedTorso != null)
            {
                appliedTorso.localPosition = savedLocalPosition;
                appliedTorso.localRotation = savedLocalRotation;
            }
            poseApplied = false;
            appliedTorso = null;
        }

        private Contact Probe(Transform foot, Vector3 offset)
        {
            Contact result = default;
            if (foot == null) return result;
            Vector3 sole = foot.TransformPoint(offset);
            int count = Physics.RaycastNonAlloc(sole + Vector3.up * 0.3f, Vector3.down,
                hits, 1f, groundMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return result;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.normal.y < 0.45f || hit.distance >= nearest) continue;
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                float signedHeight = Vector3.Dot(sole - hit.point, hit.normal);
                // Tolerate the capsule skin (about 2.7cm at current model scale),
                // but do not project a boot shadow onto a nearby raised box top.
                if (signedHeight < -0.04f) continue;
                nearest = hit.distance;
                result.valid = true;
                result.point = hit.point;
                result.normal = hit.normal;
                result.height = Mathf.Max(0f, signedHeight);
            }
            return result;
        }

        private void UpdateShadow(int index, Contact contact, bool grounded)
        {
            float alpha = IntercomGroundMath.ContactAlpha(contact.height, shadowFadeHeight,
                shadowOpacity, grounded && contact.valid);
            MeshRenderer renderer = shadowRenderers[index];
            renderer.enabled = alpha > 0.002f;
            if (!renderer.enabled) return;
            Transform item = shadows[index];
            Vector3 tangent = Vector3.ProjectOnPlane(motor.FacingDirection, contact.normal).normalized;
            if (tangent.sqrMagnitude < 0.1f) tangent = Vector3.ProjectOnPlane(Vector3.forward, contact.normal).normalized;
            item.SetPositionAndRotation(contact.point + contact.normal * Mathf.Max(0.002f, surfaceOffset),
                Quaternion.LookRotation(tangent, contact.normal));
            float softness = 1f + Mathf.Min(contact.height, shadowFadeHeight) * 0.8f;
            item.localScale = new Vector3(Mathf.Max(0.02f, shadowSize.x) * softness, 1f,
                Mathf.Max(0.02f, shadowSize.y) * softness);
            SetTint(renderer, new Color(0.08f, 0.105f, 0.12f, alpha));
        }

        private void EmitDust(Contact contact)
        {
            if (dust == null) return;
            for (int i = 0; i < 3; i++)
            {
                int slot = nextPuff++ % dust.Length;
                Vector3 drift = Quaternion.AngleAxis(i * 120f + nextPuff * 37f, contact.normal)
                    * Vector3.ProjectOnPlane(motor.FacingDirection, contact.normal).normalized;
                dust[slot].origin = contact.point + contact.normal * 0.02f;
                dust[slot].drift = drift * (0.15f + i * 0.035f) + contact.normal * 0.04f;
                dust[slot].age = 0f;
                dust[slot].bornFrame = Time.frameCount;
                dust[slot].transform.position = dust[slot].origin;
                dust[slot].transform.localScale = Vector3.one * 0.055f;
                SetTint(dust[slot].renderer, dustColor);
                dust[slot].active = true;
                dust[slot].renderer.enabled = true;
                dust[slot].transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(motor.FacingDirection, contact.normal), contact.normal);
            }
        }

        private void UpdateDust(float dt)
        {
            if (dust == null) return;
            for (int i = 0; i < dust.Length; i++)
            {
                if (!dust[i].active) continue;
                if (dust[i].bornFrame == Time.frameCount) continue;
                dust[i].age += dt;
                float life = dust[i].age / 0.23f;
                if (life >= 1f)
                {
                    dust[i].active = false;
                    dust[i].renderer.enabled = false;
                    continue;
                }
                dust[i].transform.position = dust[i].origin + dust[i].drift * life;
                dust[i].transform.localScale = Vector3.one * Mathf.Lerp(0.055f, 0.19f, life);
                Color color = dustColor; color.a *= (1f - life) * (1f - life);
                SetTint(dust[i].renderer, color);
            }
        }

        private void SetTint(Renderer renderer, Color color)
        {
            properties.Clear();
            properties.SetColor(TintId, color);
            renderer.SetPropertyBlock(properties);
        }

        private void CreateEffects()
        {
            effectsRoot = new GameObject("Intercom Ground Feedback Effects");
            effectsRoot.AddComponent<IntercomOccluder>().hideWhenBlocking = false;
            discMesh = MakeQuad();
            shadows = new Transform[2];
            shadowRenderers = new MeshRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                shadowRenderers[i] = CreateDisc("Boot Contact " + i);
                shadows[i] = shadowRenderers[i].transform;
            }
            dust = new Puff[DustCapacity];
            for (int i = 0; i < dust.Length; i++)
            {
                MeshRenderer renderer = CreateDisc("Contact Dust " + i);
                dust[i] = new Puff { renderer = renderer, transform = renderer.transform };
            }
        }

        private MeshRenderer CreateDisc(string label)
        {
            GameObject item = new GameObject(label);
            item.transform.SetParent(effectsRoot.transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = discMesh;
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = contactMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return renderer;
        }

        private static Mesh MakeQuad()
        {
            Mesh mesh = new Mesh { name = "Intercom Radial Contact Quad" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>Call in a known idle pose in the editor, or once in Awake for older scene components.</summary>
        public void CalibrateSoles()
        {
            if (animator == null) return;
            bool left = FindSole("tripo_part_3_fixed", leftFoot, out Vector3 leftOffset);
            bool right = FindSole("tripo_part_2_fixed", rightFoot, out Vector3 rightOffset);
            if (left) leftSoleOffset = leftOffset;
            if (right) rightSoleOffset = rightOffset;
            soleOffsetsCalibrated = left && right;
        }

        private bool FindSole(string meshName, Transform foot, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (foot == null) return false;
            foreach (SkinnedMeshRenderer renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != meshName) continue;
                Mesh mesh = new Mesh();
                renderer.BakeMesh(mesh);
                Vector3[] vertices = mesh.vertices;
                Vector3 min = Vector3.one * float.PositiveInfinity;
                Vector3 max = Vector3.one * float.NegativeInfinity;
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 point = renderer.transform.TransformPoint(vertex);
                    min = Vector3.Min(min, point); max = Vector3.Max(max, point);
                }
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                if (vertices.Length == 0) return false;
                offset = foot.InverseTransformPoint(new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f));
                return true;
            }
            return false;
        }

        private void Subscribe()
        {
            if (subscribedAudio != audioSource)
            {
                if (subscribedAudio != null) subscribedAudio.FootContact -= OnFootContact;
                subscribedAudio = audioSource;
                if (subscribedAudio != null) subscribedAudio.FootContact += OnFootContact;
            }
            if (subscribedShooter != shooter)
            {
                if (subscribedShooter != null) subscribedShooter.VolleyFired -= OnVolley;
                subscribedShooter = shooter;
                if (subscribedShooter != null) subscribedShooter.VolleyFired += OnVolley;
            }
        }
        private void OnFootContact() { if (Active) pendingContact = true; }
        private void OnVolley(Vector3 position, int barrels) { if (Active && barrels > 0) recoil = 1f; }
        private void ClearTransientState()
        {
            pendingContact = false;
            compression = recoil = 0f;
            fallbackClock.Reset();
        }
        private void HideEffects()
        {
            if (shadowRenderers != null) foreach (MeshRenderer renderer in shadowRenderers) renderer.enabled = false;
            if (dust != null) for (int i = 0; i < dust.Length; i++)
            { dust[i].active = false; dust[i].renderer.enabled = false; }
        }
        private void OnDisable()
        {
            RestorePose(); ClearTransientState(); HideEffects();
            if (subscribedAudio != null) subscribedAudio.FootContact -= OnFootContact;
            if (subscribedShooter != null) subscribedShooter.VolleyFired -= OnVolley;
            subscribedAudio = null; subscribedShooter = null;
        }
        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) { RestorePose(); ClearTransientState(); HideEffects(); }
        }
        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (value) { RestorePose(); ClearTransientState(); HideEffects(); }
        }
        private void OnDestroy()
        {
            RestorePose();
            if (effectsRoot != null) Destroy(effectsRoot);
            if (discMesh != null) Destroy(discMesh);
        }
    }
}
