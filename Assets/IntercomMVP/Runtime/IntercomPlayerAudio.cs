using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Bounded player SFX and a quiet loop, independent of the distant overhead camera.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    public sealed class IntercomPlayerAudio : MonoBehaviour
    {
        public IntercomAudioProfile profile;
        public IntercomSimpleShooter shooter;
        public IntercomPlayerMotor motor;
        public Animator animator;

        /// <summary>A grounded locomotion contact, emitted even when no footstep clip is assigned.</summary>
        public event System.Action FootContact;

        private const int ShotVoices = 6;
        private const int StepVoices = 2;
        private const int ImpactVoices = 4;
        private readonly IntercomFootstepClock stepClock = new IntercomFootstepClock();
        private AudioSource[] shots;
        private AudioSource[] steps;
        private AudioSource[] impacts;
        private AudioSource ambient;
        private GameObject audioRoot;
        private IntercomSimpleShooter subscribedShooter;
        private IntercomAudioProfile observedProfile;
        private int nextShot;
        private int nextStep;
        private int nextImpact;
        private int lastImpactClip = -1;
        private float lastImpactTime = float.NegativeInfinity;
        private int lastShotClip = -1;
        private int lastStepClip = -1;
        private bool hasFocus = true;
        private bool appPaused;
        private bool suspended;
        private bool ambientPaused;
        private float ambienceGain;

        private bool CanPlay => isActiveAndEnabled && hasFocus && !appPaused
            && Time.timeScale > 0f && !AudioListener.pause && profile != null;

        private void Awake()
        {
            if (shooter == null) shooter = GetComponent<IntercomSimpleShooter>();
            if (motor == null) motor = GetComponent<IntercomPlayerMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            audioRoot = new GameObject("Intercom Audio Voices");
            audioRoot.transform.SetParent(transform, false);
            shots = CreateVoices("Pistol", ShotVoices, 64);
            steps = CreateVoices("Boot", StepVoices, 128);
            impacts = CreateVoices("Impact", ImpactVoices, 112);
            ambient = CreateVoice("Yard Ambience", 200);
            ambient.loop = true;
            ambient.volume = 0f;
        }

        private void OnEnable() { Subscribe(); }

        private void OnDisable()
        {
            Unsubscribe();
            StopVoices(shots);
            StopVoices(steps);
            StopVoices(impacts);
            lastImpactTime = float.NegativeInfinity;
            if (ambient != null) ambient.Stop();
            ambienceGain = 0f;
            ambientPaused = suspended = false;
            stepClock.Reset();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (audioRoot != null) Destroy(audioRoot);
        }

        private void Subscribe()
        {
            if (subscribedShooter == shooter) return;
            Unsubscribe();
            subscribedShooter = shooter;
            if (subscribedShooter != null)
            {
                subscribedShooter.VolleyFired += OnVolleyFired;
                subscribedShooter.ProjectileHit += OnProjectileHit;
            }
        }

        private void Unsubscribe()
        {
            if (subscribedShooter != null)
            {
                subscribedShooter.VolleyFired -= OnVolleyFired;
                subscribedShooter.ProjectileHit -= OnProjectileHit;
            }
            subscribedShooter = null;
        }

        private void OnVolleyFired(Vector3 position, int barrelCount)
        {
            if (!CanPlay || shots == null || barrelCount <= 0) return;
            ObserveProfile();
            AudioClip clip = PickClip(profile.pistolShots, ref lastShotClip);
            if (clip == null) return;
            AudioSource voice = NextVoice(shots, ref nextShot);
            voice.transform.position = position;
            PlayVoice(voice, clip, profile.shotPitchVariation,
                Mathf.Clamp01(profile.masterVolume) * Mathf.Clamp01(profile.shotsVolume));
        }

        private void OnProjectileHit(Vector3 position, Vector3 normal, bool targetHit)
        {
            if (!CanPlay || impacts == null) return;
            ObserveProfile();
            float now = Time.unscaledTime;
            // Two pistol rounds may arrive on the same frame. One short report
            // keeps rapid collisions readable without adding an audio burst.
            if (now - lastImpactTime < Mathf.Max(0.015f, profile.minimumImpactInterval)) return;
            Vector3 player = motor != null ? motor.transform.position : transform.position;
            float gain = Mathf.Clamp01(1f - Vector3.Distance(position, player)
                / Mathf.Max(1f, profile.impactAudibleRange));
            if (gain <= 0f) return;
            AudioClip clip = PickClip(profile.impactHits, ref lastImpactClip);
            if (clip == null) return;
            lastImpactTime = now;
            AudioSource voice = NextVoice(impacts, ref nextImpact);
            voice.transform.position = position;
            PlayVoice(voice, clip, 0.045f,
                gain * Mathf.Clamp01(profile.masterVolume) * Mathf.Clamp01(profile.impactsVolume),
                targetHit ? 1.12f : 0.94f);
        }

        private void LateUpdate()
        {
            Subscribe();
            if (!CanPlay)
            {
                Suspend();
                return;
            }
            ObserveProfile();
            suspended = false;
            UpdateMix();
            UpdateAmbience();
            UpdateSteps();
        }

        private void ObserveProfile()
        {
            if (observedProfile == profile) return;
            observedProfile = profile;
            lastShotClip = lastStepClip = lastImpactClip = -1;
            StopVoices(shots);
            StopVoices(steps);
            StopVoices(impacts);
            lastImpactTime = float.NegativeInfinity;
            stepClock.Reset();
            if (ambient != null) ambient.Stop();
            ambientPaused = false;
            ambienceGain = 0f;
        }

        private void UpdateMix()
        {
            float master = Mathf.Clamp01(profile.masterVolume);
            float shotVolume = master * Mathf.Clamp01(profile.shotsVolume);
            float stepVolume = master * Mathf.Clamp01(profile.stepsVolume);
            for (int i = 0; i < shots.Length; i++) shots[i].volume = shotVolume;
            for (int i = 0; i < steps.Length; i++) steps[i].volume = stepVolume;
            Vector3 player = motor != null ? motor.transform.position : transform.position;
            for (int i = 0; i < impacts.Length; i++)
                impacts[i].volume = master * Mathf.Clamp01(profile.impactsVolume)
                    * Mathf.Clamp01(1f - Vector3.Distance(impacts[i].transform.position, player)
                        / Mathf.Max(1f, profile.impactAudibleRange));
        }

        private void UpdateAmbience()
        {
            if (ambient == null) return;
            if (ambient.clip != profile.ambience)
            {
                ambient.Stop();
                ambient.clip = profile.ambience;
                ambientPaused = false;
                ambienceGain = 0f;
            }
            if (ambient.clip == null) return;
            if (ambientPaused)
            {
                ambient.UnPause();
                ambientPaused = false;
            }
            if (!ambient.isPlaying) ambient.Play();
            ambienceGain = Mathf.MoveTowards(ambienceGain, 1f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, profile.fadeSeconds));
            ambient.volume = ambienceGain * Mathf.Clamp01(profile.masterVolume)
                * Mathf.Clamp01(profile.ambienceVolume);
        }

        private void UpdateSteps()
        {
            bool moving = motor != null && motor.isActiveAndEnabled && motor.IsGrounded
                && motor.ActualPlanarSpeed >= Mathf.Max(0.01f, profile.minimumStepSpeed)
                && animator != null && animator.isActiveAndEnabled && animator.speed > 0f
                && animator.runtimeAnimatorController != null && !animator.IsInTransition(0);
            if (!moving) { stepClock.Reset(); return; }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            // Follow the base locomotion cycle, not the independent Arms recoil.
            // Phase offset is tunable because directional blends approximate contact.
            if (!stepClock.Tick(state.normalizedTime, state.fullPathHash, profile.footstepPhaseOffset, true)) return;
            FootContact?.Invoke();
            AudioClip clip = PickClip(profile.footsteps, ref lastStepClip);
            if (clip == null) return;
            AudioSource voice = NextVoice(steps, ref nextStep);
            PlayVoice(voice, clip, profile.stepPitchVariation,
                Mathf.Clamp01(profile.masterVolume) * Mathf.Clamp01(profile.stepsVolume));
        }

        private void Suspend()
        {
            stepClock.Reset();
            if (suspended) return;
            suspended = true;
            StopVoices(shots);
            StopVoices(steps);
            StopVoices(impacts);
            lastImpactTime = float.NegativeInfinity;
            if (ambient != null && ambient.isPlaying)
            {
                ambient.Pause();
                ambientPaused = true;
            }
            ambienceGain = 0f;
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused) Suspend();
        }

        private void OnApplicationPause(bool paused)
        {
            appPaused = paused;
            if (paused) Suspend();
        }

        private AudioSource[] CreateVoices(string label, int count, int priority)
        {
            var voices = new AudioSource[count];
            for (int i = 0; i < count; i++) voices[i] = CreateVoice(label + " " + (i + 1), priority);
            return voices;
        }

        private AudioSource CreateVoice(string label, int priority)
        {
            var voiceObject = new GameObject(label);
            voiceObject.transform.SetParent(audioRoot.transform, false);
            AudioSource voice = voiceObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.dopplerLevel = 0f;
            voice.priority = priority;
            voice.ignoreListenerPause = false;
            return voice;
        }

        private static AudioSource NextVoice(AudioSource[] voices, ref int next)
        {
            for (int offset = 0; offset < voices.Length; offset++)
            {
                int index = (next + offset) % voices.Length;
                if (voices[index].isPlaying) continue;
                next = (index + 1) % voices.Length;
                return voices[index];
            }
            // Future automatic weapons reuse the oldest slot; voice count stays bounded.
            AudioSource oldest = voices[next];
            next = (next + 1) % voices.Length;
            return oldest;
        }

        private static void PlayVoice(AudioSource voice, AudioClip clip, float variation, float volume, float basePitch = 1f)
        {
            voice.Stop();
            voice.clip = clip;
            float spread = Mathf.Clamp(variation, 0f, 0.2f);
            voice.pitch = basePitch + Random.Range(-spread, spread);
            voice.volume = volume;
            voice.Play();
        }

        private static AudioClip PickClip(AudioClip[] clips, ref int previous)
        {
            if (clips == null || clips.Length == 0) return null;
            int first = Random.Range(0, clips.Length);
            if (clips.Length > 1 && first == previous) first = (first + 1) % clips.Length;
            for (int offset = 0; offset < clips.Length; offset++)
            {
                int index = (first + offset) % clips.Length;
                if (clips[index] == null) continue;
                previous = index;
                return clips[index];
            }
            return null;
        }

        private static void StopVoices(AudioSource[] voices)
        {
            if (voices == null) return;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i] != null) voices[i].Stop();
        }
    }
}
