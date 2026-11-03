using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Accepted health damage drives a small chassis spring and one pre-created audio voice.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(140)]
    public sealed class IntercomPlayerHurtFeedback : MonoBehaviour
    {
        public IntercomHealth health;
        public IntercomPlayerVitality vitality;
        public IntercomPlayerMotor motor;
        [Header("Chassis reaction — degrees, never a movement lock")]
        [Range(0f, 6f)] public float pitchDegrees = 5.5f;
        [Range(0f, 4.5f)] public float rollDegrees = 4f;
        [Min(1f)] public float fullStrengthDamage = 20f;
        [Header("Soft chassis impacts")]
        public AudioClip[] hurtClips;
        [Range(0f, 1f)] public float volume = .38f;
        [Range(.08f, .5f)] public float soundCooldown = .16f;

        public float HurtPitch => response.Pitch;
        public float HurtRoll => response.Roll;
        private readonly IntercomHurtResponse response = new IntercomHurtResponse();
        private IntercomHealth subscribedHealth;
        private IntercomPlayerMotor observedMotor;
        private uint observedRespawn;
        private AudioSource voice;
        private GameObject audioObject;
        private int nextClip;
        private float lastSound = float.NegativeInfinity;
        private bool focused = true, paused;

        private bool ActiveContext => isActiveAndEnabled && focused && !paused && Time.timeScale > 0f
            && health != null && health.isActiveAndEnabled;
        private bool Available => ActiveContext && (vitality == null || !vitality.MissionBlocked);

        private void Awake()
        {
            if (health == null) health = GetComponent<IntercomHealth>();
            if (vitality == null) vitality = GetComponent<IntercomPlayerVitality>();
            if (motor == null) motor = GetComponent<IntercomPlayerMotor>();
            observedMotor = motor;
            observedRespawn = motor != null ? motor.RespawnVersion : 0;
            // Created once at scene initialization, never on a hit.
            audioObject = new GameObject("Player Chassis Hurt");
            audioObject.transform.SetParent(transform, false);
            voice = audioObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;
            voice.dopplerLevel = 0f;
            voice.priority = 100;
        }

        private void OnEnable() { BindHealth(); Clear(); }

        private void BindHealth()
        {
            if (subscribedHealth == health) return;
            Unsubscribe();
            Clear();
            subscribedHealth = health;
            if (subscribedHealth != null) subscribedHealth.Damaged += OnDamaged;
        }

        private void Update()
        {
            BindHealth();
            uint respawn = motor != null ? motor.RespawnVersion : 0;
            if (observedMotor != motor || observedRespawn != respawn)
            {
                observedMotor = motor; observedRespawn = respawn; Clear();
            }
            if (!ActiveContext || AudioListener.pause) { Clear(); return; }
            // Died may synchronously open the mission result screen after Damaged.
            // Let that last thud finish, but never leave a twitching pose on a dead hero.
            if (health.IsDead) { response.Reset(); return; }
            if (vitality != null && vitality.MissionBlocked) { Clear(); return; }
            response.Advance(Time.deltaTime);
        }

        private void OnDamaged(IntercomDamage damage)
        {
            if (!Available || subscribedHealth != health || !Finite(damage.Amount) || damage.Amount <= 0f) return;
            float referenceDamage = Finite(fullStrengthDamage) ? Mathf.Max(1f, fullStrengthDamage) : 20f;
            float strength = Mathf.Clamp(Mathf.Sqrt(damage.Amount / referenceDamage), .35f, 1f);
            Vector3 forward = motor != null ? motor.FacingDirection : transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
            Vector3 direction = Vector3.ProjectOnPlane(damage.Direction, Vector3.up);
            if (direction.sqrMagnitude < .0001f && damage.Instigator != null)
                direction = Vector3.ProjectOnPlane(transform.position - damage.Instigator.transform.position, Vector3.up);
            direction = direction.sqrMagnitude < .0001f ? -forward : direction.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float pitch = Mathf.Clamp(Finite(pitchDegrees) ? pitchDegrees : 0f, 0f, 6f);
            float roll = Mathf.Clamp(Finite(rollDegrees) ? rollDegrees : 0f, 0f, 4.5f);
            response.Push(Vector3.Dot(direction, forward) * pitch * strength,
                -Vector3.Dot(direction, right) * roll * strength);
            PlayImpact(strength);
        }

        private void PlayImpact(float strength)
        {
            if (voice == null || AudioListener.pause || hurtClips == null || hurtClips.Length == 0) return;
            float now = Time.time;
            float cooldown = Finite(soundCooldown) ? Mathf.Clamp(soundCooldown, .08f, .5f) : .16f;
            if (!Finite(now) || now - lastSound < cooldown) return;
            AudioClip clip = null;
            for (int i = 0; i < hurtClips.Length; i++)
            {
                nextClip %= hurtClips.Length;
                clip = hurtClips[nextClip++];
                if (clip != null) break;
            }
            if (clip == null) return;
            lastSound = now;
            // A single replaceable voice bounds overlap even if immunity is disabled.
            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(Finite(volume) ? volume : 0f) * Mathf.Lerp(.75f, 1f, strength);
            voice.pitch = nextClip % 2 == 0 ? 1.025f : .985f;
            voice.Play();
        }

        private void Clear()
        {
            response.Reset(); lastSound = float.NegativeInfinity;
            if (voice != null) voice.Stop();
        }
        private void Unsubscribe()
        {
            if (subscribedHealth != null) subscribedHealth.Damaged -= OnDamaged;
            subscribedHealth = null;
        }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) Clear(); }
        private void OnApplicationPause(bool value) { paused = value; if (value) Clear(); }
        private void OnDisable() { Unsubscribe(); Clear(); }
        private void OnDestroy() { Unsubscribe(); if (audioObject != null) Destroy(audioObject); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
