using UnityEngine;

namespace IntercomMvp
{
    /// <summary>An authored arrival location. Optional hatch/warning transforms are reused, never instantiated on a spawn.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomEnemySpawnPoint : MonoBehaviour
    {
        public IntercomEnemySpawnKind kind = IntercomEnemySpawnKind.Ground;
        [Min(2.5f)] public float minimumDistance = 5.5f;
        [Min(0f)] public float cooldownSeconds = 4f;
        public Transform hatchLid;
        public Transform warningVisual;

        public bool IsAnimating => animating;
        public float ReadyAt => readyAt;
        private float readyAt, elapsed, duration;
        private bool animating, captured, focused=true, paused;
        private Quaternion lidRest;
        private Vector3 warningRest;

        public bool IsAvailable(float now)
            => isActiveAndEnabled && !animating && IntercomEnemySpawnPolicy.CooldownReady(now, readyAt);

        public void BeginArrival(float now, float seconds)
        {
            CaptureRest(); RestoreVisual();
            duration = IntercomNpcDefinition.Safe(seconds, 1.03f, .3f, 4f);
            elapsed = 0f; animating = true; focused=Application.isFocused; paused=false;
            readyAt = IntercomEnemySpawnPolicy.NextReadyAt(now,
                Mathf.Max(duration, IntercomNpcDefinition.Safe(cooldownSeconds, 4f, 0f, 60f)));
            if (warningVisual != null) warningVisual.gameObject.SetActive(true);
            ApplyVisual();
        }

        public void ResetForMission()
        {
            CaptureRest(); animating = false; elapsed = 0f; readyAt = 0f;
            RestoreVisual();
        }

        private void CaptureRest()
        {
            if (captured) return;
            lidRest = hatchLid != null ? hatchLid.localRotation : Quaternion.identity;
            warningRest = warningVisual != null ? warningVisual.localScale : Vector3.one;
            captured = true;
        }

        private void Update()
        {
            if (!animating || !focused || paused || Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            elapsed = Mathf.Min(duration, elapsed + Mathf.Min(dt, .1f));
            ApplyVisual();
            if (elapsed >= duration) { animating = false; RestoreVisual(); }
        }

        private void ApplyVisual()
        {
            float p = Mathf.Clamp01(elapsed / duration);
            if (kind == IntercomEnemySpawnKind.Hatch && hatchLid != null)
            {
                float opening = Mathf.Clamp01(p / .22f);
                float closing = Mathf.Clamp01((p - .88f) / .12f);
                float open = opening * opening * (3f - 2f * opening) * (1f - closing * closing * (3f - 2f * closing));
                hatchLid.localRotation = lidRest * Quaternion.Euler(-76f * open, 0f, 0f);
            }
            if (warningVisual != null)
            {
                float pulse = 1f + .09f * Mathf.Sin(p * Mathf.PI * 6f);
                warningVisual.localScale = warningRest * pulse;
            }
        }

        private void RestoreVisual()
        {
            if (hatchLid != null) hatchLid.localRotation = lidRest;
            if (warningVisual != null)
            {
                warningVisual.localScale = warningRest;
                warningVisual.gameObject.SetActive(false);
            }
        }

        private void OnApplicationFocus(bool value) { focused=value; }
        private void OnApplicationPause(bool value) { paused=value; }
        private void OnDisable() { if (captured) { animating = false; RestoreVisual(); } }
    }
}
