using System;
using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Quiet mechanical cues observed from pooled actors; gameplay and hit timing remain owned by the NPC.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(140)]
    public sealed class IntercomEnemyAudio : MonoBehaviour
    {
        public IntercomMissionDirector mission;
        [Range(0f, 1f)] public float volume = .24f;
        [Range(5f, 40f)] public float audibleRange = 22f;
        public int ReportsPlayed { get; private set; }
        public int PreparedVoices => pool == null ? 0 : VoiceCount;

        private const int ActorCount = 8, VoiceCount = 4, KindCount = 4, Variants = 3;
        private const int Arrival = 0, Windup = 1, Strike = 2, Death = 3;
        private sealed class ActorWatch
        {
            internal IntercomNpcController actor;
            internal IntercomHealth health;
            internal uint life, healthRevision, run, strike;
            internal IntercomNpcPhase phase;
            internal Vector3 position;
            internal bool seen, emerging, deathPlayed;
            internal Action deathHandler;
        }
        private readonly ActorWatch[] actors = new ActorWatch[ActorCount];
        private readonly AudioSource[] voices = new AudioSource[VoiceCount];
        private readonly AudioClip[] clips = new AudioClip[KindCount * Variants];
        private readonly float[] remaining = new float[VoiceCount], nextKindAt = new float[KindCount];
        private readonly int[] voiceKinds = new int[VoiceCount], lastVariants = { -1, -1, -1, -1 };
        private GameObject pool;
        private bool focused = true, applicationPaused, hasRun;
        private uint observedRun, sequence;
        private float nextQuietAt;
        private bool ActiveMission => mission != null && mission.isActiveAndEnabled && mission.Phase == IntercomMissionPhase.Active;
        private bool Audible => isActiveAndEnabled && focused && Application.isFocused && !applicationPaused
            && Time.timeScale > 0f && Finite(Time.time) && mission != null && mission.isActiveAndEnabled
            && mission.playerHealth != null && !mission.playerHealth.IsDead;

        private void Awake()
        {
            focused = Application.isFocused;
            if (mission == null) mission = GetComponent<IntercomMissionDirector>();
            Prepare();
        }
        private void OnEnable() { focused = Application.isFocused; applicationPaused = false; }
        private void Prepare()
        {
            if (pool != null) return;
            // Unity native resources are created on the main-thread lifecycle, not while deserializing the component.
            pool = new GameObject("Enemy mechanical cues - four reusable voices");
            pool.transform.SetParent(transform, true);
            for (int i = 0; i < VoiceCount; i++)
            {
                var child = new GameObject("Enemy cue voice " + i); child.transform.SetParent(pool.transform, true);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = source.loop = false; source.spatialBlend = .70f;
                source.dopplerLevel = 0f; source.priority = 180;
                source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 8f; source.maxDistance = 42f;
                voices[i] = source;
            }
            for (int kind = 0; kind < KindCount; kind++)
                for (int variant = 0; variant < Variants; variant++) clips[kind * Variants + variant] = BuildClip(kind, variant);
            for (int i = 0; i < ActorCount; i++)
            {
                var watch = new ActorWatch();
                watch.deathHandler = () => OnActorDied(watch);
                actors[i] = watch;
            }
        }
        private void ObserveRun()
        {
            if (mission == null || (hasRun && observedRun == mission.RunRevision)) return;
            hasRun = true; observedRun = mission.RunRevision; sequence = 0; ReportsPlayed = 0;
            UnbindAll(); StopAll(); nextQuietAt = float.NegativeInfinity;
            for (int i = 0; i < KindCount; i++) { nextKindAt[i] = float.NegativeInfinity; lastVariants[i] = -1; }
        }
        private void Update()
        {
            Prepare(); ObserveRun();
            float dt = Finite(Time.deltaTime) ? Mathf.Max(0f, Time.deltaTime) : 0f;
            if (Audible) for (int i = 0; i < VoiceCount; i++) remaining[i] = Mathf.Max(0f, remaining[i] - dt);
            if (!ActiveMission)
            {
                UnbindAll();
                // A final accepted death may leave a short tail underneath the victory card; other menus are silent.
                if (!Audible || mission == null || mission.Phase != IntercomMissionPhase.Victory) StopAll();
                else for (int i = 0; i < VoiceCount; i++) if (voiceKinds[i] != Death)
                { if (voices[i].isPlaying) voices[i].Stop(); remaining[i] = 0f; }
                return;
            }
            bool canPlay = Audible;
            if (!canPlay) StopAll();
            for (int i = 0; i < ActorCount; i++)
            {
                ActorWatch watch = actors[i]; watch.seen = false;
                if (watch.actor != null && (!watch.actor.isActiveAndEnabled || !watch.actor.transform.IsChildOf(mission.transform))) Unbind(watch);
            }
            int count = Mathf.Min(32, IntercomNpcController.ActiveActorCount);
            for (int i = 0; i < count; i++)
            {
                IntercomNpcController actor = IntercomNpcController.GetActiveActor(i);
                if (actor == null || !actor.isActiveAndEnabled || actor.Health == null || !actor.transform.IsChildOf(mission.transform)) continue;
                ActorWatch watch = Find(actor); if (watch == null) continue;
                bool fresh = watch.actor != actor || watch.life != actor.LifeVersion || watch.health != actor.Health
                    || watch.healthRevision != actor.Health.ResetRevision;
                if (fresh)
                {
                    Unbind(watch); watch.actor = actor; watch.health = actor.Health;
                    watch.life = actor.LifeVersion; watch.healthRevision = actor.Health.ResetRevision; watch.run = observedRun;
                    watch.strike = actor.ResolvedStrikeVersion; watch.phase = actor.Phase;
                    watch.emerging = actor.IsEmerging; watch.position = actor.transform.position; watch.deathPlayed = false;
                    watch.health.Died += watch.deathHandler;
                    if (canPlay && watch.emerging && !watch.health.IsDead) Play(watch, Arrival);
                }
                else
                {
                    watch.position = actor.transform.position;
                    if (canPlay && !watch.health.IsDead)
                    {
                        if (actor.IsEmerging && !watch.emerging) Play(watch, Arrival);
                        if (!actor.IsEmerging && actor.Phase == IntercomNpcPhase.Windup && watch.phase != IntercomNpcPhase.Windup) Play(watch, Windup);
                        if (actor.ResolvedStrikeVersion != watch.strike) Play(watch, Strike);
                    }
                }
                watch.seen = true; watch.strike = actor.ResolvedStrikeVersion; watch.phase = actor.Phase; watch.emerging = actor.IsEmerging;
                if (canPlay && watch.health.IsDead && !watch.deathPlayed) OnActorDied(watch);
            }
            for (int i = 0; i < ActorCount; i++) if (!actors[i].seen) Unbind(actors[i]);
        }
        private ActorWatch Find(IntercomNpcController actor)
        {
            ActorWatch available = null;
            for (int i = 0; i < ActorCount; i++)
            { if (actors[i].actor == actor) return actors[i]; if (actors[i].actor == null && available == null) available = actors[i]; }
            return available;
        }
        private void OnActorDied(ActorWatch watch)
        {
            if (watch.deathPlayed || watch.health == null || !watch.health.IsDead || watch.health.ResetRevision != watch.healthRevision
                || mission == null || watch.run != mission.RunRevision || !Audible
                || (mission.Phase != IntercomMissionPhase.Active && mission.Phase != IntercomMissionPhase.Victory)) return;
            // The mission may already have parked this actor in an earlier Died subscriber.
            // The captured health lifetime and last position remain valid; a reused health lifetime is rejected above.
            watch.deathPlayed = true; Play(watch, Death);
        }
        private bool Play(ActorWatch watch, int kind)
        {
            if (!Audible || pool == null || watch.run != mission.RunRevision) return false;
            float now = Time.time;
            if (now < nextKindAt[kind] || (kind < Strike && now < nextQuietAt)) return false;
            Vector3 delta = watch.position - mission.playerHealth.transform.position; delta.y = 0f;
            if (!Finite(delta.x) || !Finite(delta.z)) return false;
            float range = Finite(audibleRange) ? Mathf.Clamp(audibleRange, 5f, 40f) : 22f;
            if (delta.sqrMagnitude >= range * range) return false;
            int voice = -1, weakest = -1;
            for (int i = 0; i < VoiceCount; i++)
            {
                if (remaining[i] <= 0f || !voices[i].isPlaying) { voice = i; break; }
                if (weakest < 0 || voiceKinds[i] < voiceKinds[weakest]
                    || (voiceKinds[i] == voiceKinds[weakest] && remaining[i] < remaining[weakest])) weakest = i;
            }
            if (voice < 0)
            { if (weakest < 0 || voiceKinds[weakest] > kind) return false; voice = weakest; }
            int variant = (int)((watch.life + (uint)(watch.actor != null ? watch.actor.variant : 0) + sequence++) % Variants);
            if (variant == lastVariants[kind]) variant = (variant + 1) % Variants;
            lastVariants[kind] = variant;
            float pitch = watch.actor != null && watch.actor.Archetype == IntercomNpcArchetype.Heavy ? .83f
                : watch.actor != null && watch.actor.Archetype == IntercomNpcArchetype.Runner ? 1.12f : 1f;
            pitch *= .98f + variant * .02f;
            AudioSource source = voices[voice]; source.Stop(); source.transform.position = watch.position + Vector3.up * .65f;
            source.pitch = pitch;
            float gain = 1f - Mathf.Sqrt(delta.sqrMagnitude) / range;
            float kindGain = kind == Arrival ? .58f : kind == Windup ? .66f : kind == Strike ? .82f : 1f;
            source.volume = (Finite(volume) ? Mathf.Clamp01(volume) : .24f) * kindGain * gain;
            AudioClip clip = clips[kind * Variants + variant]; source.PlayOneShot(clip);
            remaining[voice] = clip.length / Mathf.Max(.1f, pitch); voiceKinds[voice] = kind;
            nextKindAt[kind] = now + (kind == Strike ? .065f : .11f); nextQuietAt = now + .045f;
            ReportsPlayed++; return true;
        }
        private void Unbind(ActorWatch watch)
        {
            if (watch.health != null) watch.health.Died -= watch.deathHandler;
            watch.actor = null; watch.health = null; watch.seen = false;
        }
        private void UnbindAll() { for (int i = 0; i < ActorCount; i++) if (actors[i] != null) Unbind(actors[i]); }
        private void StopAll()
        { for (int i = 0; i < VoiceCount; i++) { if (voices[i] != null && voices[i].isPlaying) voices[i].Stop(); remaining[i] = 0f; } }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) StopAll(); }
        private void OnApplicationPause(bool value) { applicationPaused = value; if (value) StopAll(); }
        private void OnDisable() { UnbindAll(); StopAll(); }
        private void OnDestroy()
        {
            UnbindAll(); StopAll(); if (pool != null) Destroy(pool);
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) Destroy(clips[i]);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static AudioClip BuildClip(int kind, int variant)
        {
            const int rate = 22050;
            float duration = kind == Arrival ? .31f : kind == Windup ? .25f : kind == Strike ? .17f : .39f;
            int length = Mathf.CeilToInt(duration * rate); var data = new float[length];
            uint noise = (uint)(0x51ea7 + kind * 929 + variant * 7919);
            float low = 0f, phase = 0f, tone = .96f + variant * .045f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate, p = t / duration;
                noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                float white = (noise & 65535u) / 32767.5f - 1f;
                low += (white - low) * (kind == Strike ? .24f : .095f);
                float envelope = Mathf.Min(1f, t / .012f) * Mathf.Min(1f, (duration - t) / .035f);
                float sample;
                if (kind == Arrival)
                {
                    phase += (92f + p * 48f) * tone / rate;
                    sample = Mathf.Sin(phase * Mathf.PI * 2f) * .14f + low * .28f;
                    sample *= (1f - p) * (.60f + .40f * Mathf.Sin(p * Mathf.PI));
                }
                else if (kind == Windup)
                {
                    phase += (145f + p * 85f) * tone / rate;
                    sample = (Mathf.Sin(phase * Mathf.PI * 2f) * .12f + Mathf.Sin(phase * Mathf.PI * 4.04f) * .035f + low * .17f)
                        * (.65f + p * .35f);
                }
                else if (kind == Strike)
                {
                    float whoosh = Mathf.Sin(Mathf.Clamp01(p * 1.30f) * Mathf.PI);
                    sample = low * whoosh * .90f + Mathf.Sin(t * 235f * tone * Mathf.PI * 2f) * (1f - p) * .025f;
                }
                else
                {
                    phase += (112f - p * 28f) * tone / rate;
                    sample = (Mathf.Sin(phase * Mathf.PI * 2f) * .18f + low * .40f) * (1f - p) * (1f - p);
                    for (int tick = 0; tick < 3; tick++)
                    {
                        float age = t - (.025f + tick * .072f);
                        if (age >= 0f && age < .065f) sample += Mathf.Sin(age * (570f + tick * 170f) * tone * Mathf.PI * 2f)
                            * .075f * (1f - age / .065f) * (1f - age / .065f);
                    }
                }
                data[i] = sample * envelope;
            }
            string label = kind == Arrival ? "arrival servo" : kind == Windup ? "windup servo" : kind == Strike ? "swing whoosh" : "chassis rattle";
            AudioClip clip = AudioClip.Create("Enemy " + label + " " + (variant + 1), length, 1, rate, false);
            clip.SetData(data, 0); return clip;
        }
    }
}
