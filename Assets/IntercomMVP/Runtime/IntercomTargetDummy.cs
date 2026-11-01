using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Stationary training actor: readable hit squash, health tint and automatic reset.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomTargetDummy : MonoBehaviour
    {
        [Tooltip("Child containing the capsule mesh. Keep the collider on the unscaled parent.")]
        public Transform visual;
        public Renderer bodyRenderer;
        [Min(1)] public int maxHealth = 5;
        [Min(0.25f)] public float resetSeconds = 2.5f;
        public float Health { get; private set; }
        public bool IsDown => resetRemaining > 0f;

        private Vector3 restScale;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Collider[] colliders;
        private bool[] colliderStates;
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock originalProperties;
        private Color restColor;
        private float hitRemaining;
        private float resetRemaining;
        private Vector3 pushDirection;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            if (visual == null) visual = transform;
            if (bodyRenderer == null) bodyRenderer = visual.GetComponentInChildren<Renderer>();
            restScale = visual.localScale;
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            colliders = GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) colliderStates[i] = colliders[i].enabled;
            properties = new MaterialPropertyBlock();
            originalProperties = new MaterialPropertyBlock();
            restColor = new Color(0.32f, 0.78f, 0.72f);
            if (bodyRenderer != null)
            {
                bodyRenderer.GetPropertyBlock(originalProperties);
                Material material = bodyRenderer.sharedMaterial;
                if (material != null && material.HasProperty(BaseColorId)) restColor = material.GetColor(BaseColorId);
            }
            Health = Mathf.Max(1, maxHealth);
        }

        public bool ApplyHit(float damage, Vector3 direction)
        {
            if (!isActiveAndEnabled || IsDown || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            Health = Mathf.Max(0f, Health - damage);
            hitRemaining = 0.2f;
            pushDirection = transform.InverseTransformDirection(Vector3.ProjectOnPlane(direction, Vector3.up)).normalized;
            if (Health <= 0f)
            {
                resetRemaining = Mathf.Max(0.25f, resetSeconds);
                for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = false;
            }
            return true;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || visual == null) return;
            float dt = Time.deltaTime;
            hitRemaining = Mathf.Max(0f, hitRemaining - dt);
            if (IsDown)
            {
                resetRemaining = Mathf.Max(0f, resetRemaining - dt);
                // Fold toward the floor, keeping the training target easy to find.
                float remaining = Mathf.Clamp01(resetRemaining / Mathf.Max(0.25f, resetSeconds));
                float recover = remaining < 0.12f ? 1f - remaining / 0.12f : 0f;
                visual.localScale = Vector3.Scale(restScale, new Vector3(Mathf.Lerp(1.2f, 1f, recover), Mathf.Lerp(0.12f, 1f, recover), Mathf.Lerp(1.2f, 1f, recover)));
                if (visual != transform) visual.localPosition = new Vector3(restPosition.x, restPosition.y * Mathf.Lerp(0.12f, 1f, recover), restPosition.z);
                Tint(Color.Lerp(new Color(0.22f, 0.28f, 0.31f), restColor, recover));
                if (resetRemaining <= 0f) ResetTarget();
                return;
            }
            float phase = hitRemaining / 0.2f;
            float pulse = Mathf.Sin(phase * Mathf.PI);
            visual.localScale = Vector3.Scale(restScale, new Vector3(1f + pulse * 0.16f, 1f - pulse * 0.14f, 1f + pulse * 0.16f));
            visual.localRotation = restRotation * Quaternion.Euler(pushDirection.z * pulse * 10f, 0f, -pushDirection.x * pulse * 10f);
            if (visual != transform) visual.localPosition = restPosition + pushDirection * (pulse * 0.055f);
            Color healthColor = Color.Lerp(new Color(1f, 0.39f, 0.18f), restColor, Health / Mathf.Max(1, maxHealth));
            Tint(Color.Lerp(healthColor, new Color(1f, 0.96f, 0.67f), Mathf.Clamp01((phase - 0.55f) * 3f)));
        }

        private void Tint(Color color)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColorId, color); properties.SetColor(ColorId, color);
            bodyRenderer.SetPropertyBlock(properties);
        }

        [ContextMenu("Reset Training Target")]
        public void ResetTarget()
        {
            if (visual == null || colliders == null) return;
            Health = Mathf.Max(1, maxHealth); resetRemaining = hitRemaining = 0f;
            visual.localScale = restScale; visual.localPosition = restPosition; visual.localRotation = restRotation;
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
            if (bodyRenderer != null) bodyRenderer.SetPropertyBlock(originalProperties);
        }

        private void OnDisable() { ResetTarget(); }
    }
}
