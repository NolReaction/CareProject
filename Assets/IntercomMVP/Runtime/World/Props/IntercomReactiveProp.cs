using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Shot-reactive low scenery. Explicit references keep sibling mesh and collision modules independent.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomReactiveProp : MonoBehaviour, IIntercomDamageable
    {
        public IntercomPropKind kind;
        [Tooltip("Mesh child or sibling. Do not assign the collider transform.")]
        public Transform visual;
        [Tooltip("Optional inactive debris variant for a broken crate. Must be a separate sibling, with no physics or damage receiver.")]
        public Transform spentVisual;
        [Tooltip("Optional relay door pivot. Its mesh extends down from a top hinge; outward is local -Z.")]
        public Transform flap;
        [Tooltip("Optional relay lamp renderer; modified through a property block, never a material instance.")]
        public Renderer indicator;
        public Collider[] blockingColliders;
        public IntercomPlayerMotor resetMotor;
        [Min(1f)] public float maxHealth = 45f;
        [Min(.25f)] public float recoverSeconds = 4f;
        [Range(30f, 88f)] public float foldAngle = 78f;
        [Tooltip("Ground clearance at the full fold. Match this to the authored bumper's base width.")]
        [Min(0f)] public float foldLift = .14f;

        public IntercomPropPhase Phase => state.Phase;
        public float Health => state.Health;
        public bool IsSpent => state.Phase == IntercomPropPhase.Spent;
        public bool IsTripped => state.Phase == IntercomPropPhase.Tripped;

        private readonly IntercomPropState state = new IntercomPropState();
        private Vector3 restPosition, restScale;
        private Quaternion restRotation, restFlapRotation;
        private bool[] originalColliders;
        private MaterialPropertyBlock lampProperties, originalLampProperties;
        private Color lampColor = new Color(.4f, .67f, .5f, 1f);
        private bool initialized;
        private bool originalVisualActive, originalSpentActive;
        private uint observedRespawn;
        private IntercomPlayerMotor observedMotor;
        private float hitRemaining, stateBlend;
        private Vector3 foldAxis = Vector3.right;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            // Assigning a collider as the animated mesh would move collision during damage.
            // Incomplete authoring is kept safe and can be diagnosed in the Inspector.
            if (visual != null && transform.IsChildOf(visual)) visual = null;
            if (spentVisual != null && (transform.IsChildOf(spentVisual)
                || (visual != null && (spentVisual.IsChildOf(visual) || visual.IsChildOf(spentVisual))))) spentVisual = null;
            if (visual != null)
            {
                originalVisualActive = visual.gameObject.activeSelf;
                restPosition = visual.localPosition;
                restRotation = visual.localRotation;
                restScale = visual.localScale;
            }
            if (spentVisual != null) originalSpentActive = spentVisual.gameObject.activeSelf;
            if (flap != null) restFlapRotation = flap.localRotation;
            if (blockingColliders == null) blockingColliders = GetComponents<Collider>();
            originalColliders = new bool[blockingColliders.Length];
            for (int i = 0; i < blockingColliders.Length; i++)
                originalColliders[i] = blockingColliders[i] != null && blockingColliders[i].enabled;
            if (indicator != null)
            {
                originalLampProperties = new MaterialPropertyBlock();
                lampProperties = new MaterialPropertyBlock();
                indicator.GetPropertyBlock(originalLampProperties);
                Material material = indicator.sharedMaterial;
                if (material != null && material.HasProperty(BaseColorId)) lampColor = material.GetColor(BaseColorId);
            }
            initialized = true;
            ResetProp();
        }

        public bool TakeDamage(IntercomDamage damage)
        {
            if (!initialized || !isActiveAndEnabled || Time.timeScale <= 0f) return false;
            ObserveRespawn();
            if (!state.TakeDamage(damage.Amount)) return false;
            hitRemaining = .2f;
            Vector3 direction = Vector3.ProjectOnPlane(damage.Direction, Vector3.up);
            if (direction.sqrMagnitude > .0001f && visual != null)
            {
                // Transform into the mesh parent's space, then the rest orientation.
                Vector3 local = visual.parent != null ? visual.parent.InverseTransformDirection(direction) : direction;
                local = Quaternion.Inverse(restRotation) * local;
                Vector3 axis = Vector3.Cross(Vector3.up, local.normalized);
                if (axis.sqrMagnitude > .0001f) foldAxis = axis.normalized;
            }
            SyncColliders();
            return true;
        }

        private void Update()
        {
            if (!initialized) return;
            ObserveRespawn();
            float dt = Time.deltaTime;
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            state.Tick(dt);
            hitRemaining = Mathf.Max(0f, hitRemaining - dt);
            float target = IsSpent || IsTripped ? 1f : 0f;
            stateBlend = Mathf.MoveTowards(stateBlend, target, dt / .25f);
            ApplyVisuals();
        }

        private void ObserveRespawn()
        {
            uint version = resetMotor != null ? resetMotor.RespawnVersion : 0;
            if (observedMotor == resetMotor && observedRespawn == version) return;
            ResetProp();
        }

        private void SyncColliders()
        {
            for (int i = 0; i < blockingColliders.Length; i++)
                if (blockingColliders[i] != null)
                    blockingColliders[i].enabled = originalColliders[i] && state.BlocksMovement;
        }

        private void ApplyVisuals()
        {
            float hit = Mathf.Sin(Mathf.Clamp01(hitRemaining / .2f) * Mathf.PI);
            float ease = stateBlend * stateBlend * (3f - 2f * stateBlend);
            if (state.Kind == IntercomPropKind.Crate && spentVisual != null)
            {
                // Keep a brief squash on the last hit, then reveal authored scattered boards.
                // This never deactivates the collision owner or the damage component itself.
                bool debris = IsSpent && stateBlend >= .75f;
                SetVisible(visual, originalVisualActive && !debris);
                SetVisible(spentVisual, originalSpentActive || debris);
            }
            if (visual != null)
            {
                if (state.Kind == IntercomPropKind.Crate)
                {
                    float damaged = Phase == IntercomPropPhase.Damaged ? 1f : 0f;
                    visual.localScale = Vector3.Scale(restScale, new Vector3(
                        1f + hit * .08f + ease * .12f, 1f - hit * .09f - ease * .86f, 1f + hit * .08f + ease * .09f));
                    visual.localRotation = restRotation * Quaternion.AngleAxis(damaged * 3f + hit * 5f + ease * 7f, foldAxis);
                    visual.localPosition = restPosition;
                }
                else if (state.Kind == IntercomPropKind.Bollard)
                {
                    float damaged = Phase == IntercomPropPhase.Damaged ? 9f : 0f;
                    float safeFold = float.IsNaN(foldAngle) || float.IsInfinity(foldAngle) ? 78f : Mathf.Clamp(foldAngle, 30f, 88f);
                    float angle = Mathf.Lerp(damaged, safeFold, ease) + hit * 10f * (1f - ease);
                    visual.localRotation = restRotation * Quaternion.AngleAxis(angle, foldAxis);
                    visual.localScale = Vector3.Scale(restScale, new Vector3(1f, 1f - hit * .08f * (1f - ease), 1f));
                    // Follow the actual tilt, including the damaged pose and hit recoil.
                    // Linear-in-time lift would still put the base through the floor halfway down.
                    float safeLift = float.IsNaN(foldLift) || float.IsInfinity(foldLift) ? .14f : Mathf.Max(0f, foldLift);
                    float lift = safeLift * Mathf.Sin(Mathf.Abs(angle) * Mathf.PI / 180f)
                        / Mathf.Sin(safeFold * Mathf.PI / 180f);
                    visual.localPosition = restPosition + Vector3.up * (lift * Mathf.Abs(restScale.y));
                }
                // Relay housing stays fixed; only its correctly hinged door moves.
            }
            if (flap != null)
                flap.localRotation = restFlapRotation * Quaternion.Euler(ease * 65f + hit * 8f * (1f - ease), 0f, 0f);
            if (indicator != null && lampProperties != null)
            {
                indicator.GetPropertyBlock(lampProperties);
                Color color = Color.Lerp(lampColor, new Color(.22f, .19f, .14f, 1f), ease);
                lampProperties.SetColor(BaseColorId, color);
                lampProperties.SetColor(ColorId, color);
                lampProperties.SetColor(EmissionId, color * ((1f - ease) * .35f));
                indicator.SetPropertyBlock(lampProperties);
            }
        }

        [ContextMenu("Reset Reactive Prop")]
        public void ResetProp()
        {
            if (!initialized) return;
            state.Reset(kind, maxHealth, recoverSeconds);
            hitRemaining = stateBlend = 0f;
            foldAxis = Vector3.right;
            observedMotor = resetMotor;
            observedRespawn = resetMotor != null ? resetMotor.RespawnVersion : 0;
            if (visual != null)
            {
                SetVisible(visual, originalVisualActive);
                visual.localPosition = restPosition;
                visual.localRotation = restRotation;
                visual.localScale = restScale;
            }
            SetVisible(spentVisual, originalSpentActive);
            if (flap != null) flap.localRotation = restFlapRotation;
            if (indicator != null && originalLampProperties != null) indicator.SetPropertyBlock(originalLampProperties);
            SyncColliders();
        }

        private void OnDisable() { ResetProp(); }

        private static void SetVisible(Transform target, bool visible)
        {
            if (target != null && target.gameObject.activeSelf != visible) target.gameObject.SetActive(visible);
        }
    }
}
