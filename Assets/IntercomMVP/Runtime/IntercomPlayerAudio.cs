using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Bounded player SFX, verified boot contacts and two quiet background loops.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    public sealed class IntercomPlayerAudio : MonoBehaviour
    {
        public IntercomAudioProfile profile;
        public IntercomSimpleShooter shooter;
        public IntercomPlayerMotor motor;
        public Animator animator;
        public IntercomGroundFeedback groundFeedback;

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
        private AudioSource music;
        private readonly float[] stepGains = new float[StepVoices];
        private readonly float[] impactGains = new float[ImpactVoices];
        private readonly RaycastHit[] groundHits = new RaycastHit[12];
        private GameObject audioRoot;
        private IntercomSimpleShooter subscribedShooter;
        private IntercomGroundFeedback subscribedGroundFeedback;
        private IntercomAudioProfile observedProfile;
        private int nextShot;
        private int nextStep;
        private int nextImpact;
        private readonly int[] lastImpactClips = { -1, -1, -1, -1, -1, -1 };
        private readonly IntercomImpactAudio impactReports = new IntercomImpactAudio();
        private int lastShotClip = -1;
        private readonly int[] lastStepClips = { -1, -1, -1, -1, -1, -1 };
        private bool hasFocus = true;
        private bool appPaused;
        private bool suspended;
        private bool ambientPaused;
        private bool musicPaused;
        private float ambienceGain;
        private float musicGain;

        private IntercomWeaponDefinition Weapon => shooter != null ? shooter.Weapon : null;
        private float ShotVolume => Weapon != null ? Weapon.shotVolume : profile.shotsVolume;
        private float ShotPitchVariation => Weapon != null ? Weapon.shotPitchVariation : profile.shotPitchVariation;
        private AudioClip[] ShotClips => Weapon != null ? Weapon.shotClips : profile.pistolShots;

        private bool CanPlay => isActiveAndEnabled && hasFocus && !appPaused
            && Time.timeScale > 0f && !AudioListener.pause && profile != null;

        private void Awake()
        {
            if (shooter == null) shooter = GetComponent<IntercomSimpleShooter>();
            if (motor == null) motor = GetComponent<IntercomPlayerMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (groundFeedback == null) groundFeedback = GetComponent<IntercomGroundFeedback>();
            audioRoot = new GameObject("Intercom Audio Voices");
            audioRoot.transform.SetParent(transform, false);
            shots = CreateVoices("Pistol", ShotVoices, 64);
            steps = CreateVoices("Boot", StepVoices, 128);
            impacts = CreateVoices("Impact", ImpactVoices, 112);
            ambient = CreateVoice("Yard Ambience", 200);
            ambient.loop = true;
            ambient.volume = 0f;
            music = CreateVoice("District Music", 192);
            music.loop = true;
            music.volume = 0f;
        }

        private void OnEnable() { Subscribe(); }

        private void OnDisable()
        {
            Unsubscribe();
            StopVoices(shots);
            StopVoices(steps);
            StopVoices(impacts);
            impactReports.Reset();
            if (ambient != null) ambient.Stop();
            if (music != null) music.Stop();
            ambienceGain = musicGain = 0f;
            ambientPaused = musicPaused = suspended = false;
            stepClock.Reset();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (audioRoot != null) Destroy(audioRoot);
        }

        private void Subscribe()
        {
            if (subscribedShooter != shooter)
            {
                UnsubscribeShooter();
                subscribedShooter = shooter;
                if (subscribedShooter != null)
                {
                    subscribedShooter.VolleyFired += OnVolleyFired;
                    subscribedShooter.DetailedProjectileHit += OnProjectileHit;
                }
            }
            if (groundFeedback == null) groundFeedback = GetComponent<IntercomGroundFeedback>();
            if (subscribedGroundFeedback == groundFeedback) return;
            if (subscribedGroundFeedback != null) subscribedGroundFeedback.Footstep -= OnFootstep;
            subscribedGroundFeedback = groundFeedback;
            if (subscribedGroundFeedback != null) subscribedGroundFeedback.Footstep += OnFootstep;
            stepClock.Reset();
        }

        private void Unsubscribe()
        {
            UnsubscribeShooter();
            if (subscribedGroundFeedback != null) subscribedGroundFeedback.Footstep -= OnFootstep;
            subscribedGroundFeedback = null;
        }

        private void UnsubscribeShooter()
        {
            if (subscribedShooter != null)
            {
                subscribedShooter.VolleyFired -= OnVolleyFired;
                subscribedShooter.DetailedProjectileHit -= OnProjectileHit;
            }
            subscribedShooter = null;
        }

        private void OnVolleyFired(Vector3 position, int barrelCount)
        {
            if (!CanPlay || shots == null || barrelCount <= 0) return;
            ObserveProfile();
            AudioClip clip = PickClip(ShotClips, ref lastShotClip);
            if (clip == null) return;
            AudioSource voice = NextVoice(shots, ref nextShot);
            voice.transform.position = position;
            PlayVoice(voice, clip, ShotPitchVariation,
                Mathf.Clamp01(profile.masterVolume) * Mathf.Clamp01(ShotVolume));
        }

        private void OnProjectileHit(IntercomProjectileImpact hit)
        {
            if (!CanPlay || impacts == null) return;
            ObserveProfile();
            float now = Time.unscaledTime;
            int bankIndex = Mathf.Clamp((int)hit.Surface, 0, lastImpactClips.Length - 1);
            // Coalesce paired rounds only within the same material. A simultaneous
            // wood/metal hit must retain both distinct reports.
            if (!impactReports.CanPlay(bankIndex, now, profile.minimumImpactInterval)) return;
            Vector3 player = motor != null ? motor.transform.position : transform.position;
            float gain = IntercomImpactAudio.DistanceGain(Vector3.Distance(hit.Point, player), profile.impactAudibleRange);
            float materialGain = Mathf.Clamp(hit.Volume, 0f, 2f);
            if (gain <= 0f || materialGain <= 0f) return;
            AudioClip[] bank = hit.Surface == IntercomSurfaceKind.Metal ? profile.metalImpacts
                : hit.Surface == IntercomSurfaceKind.Wood ? profile.woodImpacts
                : hit.Surface == IntercomSurfaceKind.Soil || hit.Surface == IntercomSurfaceKind.Gravel ? profile.soilImpacts
                : profile.concreteImpacts;
            AudioClip clip = PickClip(bank, ref lastImpactClips[bankIndex]);
            if (clip == null) clip = PickClip(profile.impactHits, ref lastImpactClips[0]);
            if (clip == null) return;
            impactReports.MarkPlayed(bankIndex, now);
            AudioSource voice = NextVoice(impacts, ref nextImpact);
            voice.transform.position = hit.Point;
            for (int i = 0; i < impacts.Length; i++)
                if (impacts[i] == voice) { impactGains[i] = materialGain; break; }
            PlayVoice(voice, clip, 0.018f,
                gain * materialGain * Mathf.Clamp01(profile.masterVolume) * Mathf.Clamp01(profile.impactsVolume),
                Mathf.Clamp(hit.Pitch, 0.6f, 1.4f));
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
            UpdateLoop(ambient, profile.ambience, profile.enableAmbience,
                profile.ambienceVolume, ref ambientPaused, ref ambienceGain);
            UpdateLoop(music, profile.musicLoop, profile.enableMusic,
                profile.musicVolume, ref musicPaused, ref musicGain);
            UpdateSteps();
        }

        private void ObserveProfile()
        {
            if (observedProfile == profile) return;
            observedProfile = profile;
            lastShotClip = -1;
            for (int i = 0; i < lastImpactClips.Length; i++) lastImpactClips[i] = -1;
            for (int i = 0; i < lastStepClips.Length; i++) lastStepClips[i] = -1;
            StopVoices(shots);
            StopVoices(steps);
            StopVoices(impacts);
            impactReports.Reset();
            stepClock.Reset();
            if (ambient != null) ambient.Stop();
            if (music != null) music.Stop();
            ambientPaused = musicPaused = false;
            ambienceGain = musicGain = 0f;
        }

        private void UpdateMix()
        {
            float master = Mathf.Clamp01(profile.masterVolume);
            float shotVolume = master * Mathf.Clamp01(ShotVolume);
            float stepVolume = master * Mathf.Clamp01(profile.stepsVolume);
            for (int i = 0; i < shots.Length; i++) shots[i].volume = shotVolume;
            for (int i = 0; i < steps.Length; i++) steps[i].volume = stepVolume * stepGains[i];
            Vector3 player = motor != null ? motor.transform.position : transform.position;
            for (int i = 0; i < impacts.Length; i++)
                impacts[i].volume = master * Mathf.Clamp01(profile.impactsVolume) * impactGains[i]
                    * IntercomImpactAudio.DistanceGain(Vector3.Distance(impacts[i].transform.position, player), profile.impactAudibleRange);
        }

        private void UpdateLoop(AudioSource source, AudioClip clip, bool enabledLoop,
            float volume, ref bool pausedLoop, ref float gain)
        {
            if (source == null) return;
            if (source.clip != clip)
            {
                source.Stop();
                source.clip = clip;
                pausedLoop = false;
                gain = 0f;
            }
            if (source.clip == null) return;
            gain = Mathf.MoveTowards(gain, enabledLoop ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, profile.fadeSeconds));
            source.volume = gain * Mathf.Clamp01(profile.masterVolume) * Mathf.Clamp01(volume);
            if (!enabledLoop)
            {
                if (gain <= 0f) { source.Stop(); pausedLoop = false; }
                return;
            }
            if (pausedLoop) { source.UnPause(); pausedLoop = false; }
            if (!source.isPlaying) source.Play();
        }

        private void UpdateSteps()
        {
            // The foot solver owns contact timing. Never layer phase-clock
            // footsteps over its real planted-sole events.
            if (groundFeedback != null && groundFeedback.ProvidesFootContacts)
            { stepClock.Reset(); return; }
            bool moving = MovingOnGround()
                && animator != null && animator.isActiveAndEnabled && animator.speed > 0f
                && animator.runtimeAnimatorController != null && !animator.IsInTransition(0);
            if (!moving) { stepClock.Reset(); return; }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            // Follow the base locomotion cycle, not the independent Arms recoil.
            // Phase offset is tunable because directional blends approximate contact.
            if (!stepClock.Tick(state.normalizedTime, state.fullPathHash, profile.footstepPhaseOffset, true)) return;
            if (ProbeLegacyGround(out RaycastHit hit)) OnFootstep(hit.collider, hit.point, 0.75f, false);
        }

        private bool MovingOnGround()
        {
            return motor != null && motor.isActiveAndEnabled && motor.IsGrounded
                && motor.ActualPlanarSpeed >= Mathf.Max(0.01f, profile.minimumStepSpeed);
        }

        private void OnFootstep(Collider surface, Vector3 point, float strength, bool leftFoot)
        {
            bool turnLanding = groundFeedback != null && groundFeedback.ProvidesFootContacts
                && groundFeedback.IsTurnFootstep && motor != null && motor.isActiveAndEnabled && motor.IsGrounded;
            if (!CanPlay || steps == null || (!MovingOnGround() && !turnLanding) || surface == null
                || surface.isTrigger || surface.transform.IsChildOf(transform)) return;
            ObserveProfile();
            IntercomFootstepSurface tag = IntercomFootstepSurface.Find(surface);
            IntercomSurfaceKind kind = tag != null ? tag.kind : IntercomSurfaceKind.Asphalt;
            int bankIndex = Mathf.Clamp((int)kind, 0, lastStepClips.Length - 1);
            AudioClip[] bank = kind == IntercomSurfaceKind.Metal ? profile.metalSteps
                : kind == IntercomSurfaceKind.Concrete ? profile.concreteSteps
                : kind == IntercomSurfaceKind.Gravel ? profile.gravelSteps
                : kind == IntercomSurfaceKind.Wood ? profile.woodSteps
                : kind == IntercomSurfaceKind.Soil ? profile.soilSteps : profile.footsteps;
            if (bank == null || bank.Length == 0) bank = profile.footsteps;
            AudioClip clip = PickClip(bank, ref lastStepClips[bankIndex]);
            if (clip == null && bank != profile.footsteps)
                clip = PickClip(profile.footsteps, ref lastStepClips[0]);
            // Keep legacy listeners informed, but the solver never subscribes
            // back to this notification: there is only one contact authority.
            FootContact?.Invoke();
            if (clip == null) return;
            AudioSource voice = NextVoice(steps, ref nextStep);
            float materialGain = kind == IntercomSurfaceKind.Gravel ? 0.84f
                : kind == IntercomSurfaceKind.Metal ? 0.88f
                : kind == IntercomSurfaceKind.Soil ? 0.82f : 1f;
            float gain = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(strength)) * materialGain
                * (tag != null ? Mathf.Clamp(tag.volume, 0f, 2f) : 1f);
            float pitch = tag != null ? Mathf.Clamp(tag.pitch, 0.6f, 1.4f) : 1f;
            for (int i = 0; i < steps.Length; i++)
                if (steps[i] == voice) { stepGains[i] = gain; break; }
            voice.transform.position = point;
            PlayVoice(voice, clip, profile.stepPitchVariation,
                Mathf.Clamp01(profile.masterVolume) * Mathf.Clamp01(profile.stepsVolume) * gain, pitch);
        }

        private bool ProbeLegacyGround(out RaycastHit closest)
        {
            closest = default;
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits,
                0.65f, ~0, QueryTriggerInteraction.Ignore);
            if (count == groundHits.Length) return false;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || hit.collider.isTrigger || hit.normal.y < 0.55f
                    || hit.collider.transform.IsChildOf(transform) || hit.distance >= distance) continue;
                closest = hit;
                distance = hit.distance;
            }
            return distance < float.PositiveInfinity;
        }

        private void Suspend()
        {
            stepClock.Reset();
            if (suspended) return;
            suspended = true;
            StopVoices(shots);
            StopVoices(steps);
            StopVoices(impacts);
            impactReports.Reset();
            if (ambient != null && ambient.isPlaying)
            {
                ambient.Pause();
                ambientPaused = true;
            }
            if (music != null && music.isPlaying)
            {
                music.Pause();
                musicPaused = true;
            }
            ambienceGain = musicGain = 0f;
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
