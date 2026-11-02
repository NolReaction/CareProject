using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Replace the prototype sounds here without changing movement or weapon code.</summary>
    [CreateAssetMenu(menuName = "Intercom MVP/Audio Profile", fileName = "IntercomAudioProfile")]
    public sealed class IntercomAudioProfile : ScriptableObject
    {
        [Header("Replaceable clips")]
        [Tooltip("A short report per successful volley. Both pistols share one report to preserve headroom.")]
        public AudioClip[] pistolShots;
        [Tooltip("Default hard-ground soles: asphalt and concrete.")]
        public AudioClip[] footsteps;
        public AudioClip[] metalSteps;
        public AudioClip[] gravelSteps;
        [Tooltip("Short impact pops; target hits are pitched up slightly.")]
        public AudioClip[] impactHits;
        [Tooltip("A quiet seamless outdoor bed, mixed below footsteps.")]
        public AudioClip ambience;
        [Tooltip("Original instrumental loop; independent of the outdoor ambience.")]
        public AudioClip musicLoop;
        public bool enableAmbience = true;
        public bool enableMusic = true;

        [Header("Mix")]
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Range(0f, 1f)] public float shotsVolume = 0.58f;
        [Range(0f, 1f)] public float stepsVolume = 0.42f;
        [Range(0f, 1f)] public float impactsVolume = 0.24f;
        [Min(0.015f)] public float minimumImpactInterval = 0.035f;
        [Min(1f)] public float impactAudibleRange = 18f;
        [Range(0f, 1f)] public float ambienceVolume = 0.05f;
        [Range(0f, 1f)] public float musicVolume = 0.20f;
        [Range(0f, 0.2f)] public float shotPitchVariation = 0.04f;
        [Range(0f, 0.2f)] public float stepPitchVariation = 0.045f;
        [Min(0.01f)] public float fadeSeconds = 0.6f;

        [Header("Foot contact")]
        [Min(0.01f)] public float minimumStepSpeed = 0.08f;
        [Tooltip("Legacy fallback only when no grounded foot-contact provider exists.")]
        [Range(0f, 0.5f)] public float footstepPhaseOffset;
    }
}
