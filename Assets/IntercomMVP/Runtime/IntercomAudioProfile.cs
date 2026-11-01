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
        public AudioClip[] footsteps;
        [Tooltip("A seamless outdoor loop; no music.")]
        public AudioClip ambience;

        [Header("Mix")]
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Range(0f, 1f)] public float shotsVolume = 0.55f;
        [Range(0f, 1f)] public float stepsVolume = 0.35f;
        [Range(0f, 1f)] public float ambienceVolume = 0.25f;
        [Range(0f, 0.2f)] public float shotPitchVariation = 0.04f;
        [Range(0f, 0.2f)] public float stepPitchVariation = 0.08f;
        [Min(0.01f)] public float fadeSeconds = 0.6f;

        [Header("Foot contact")]
        [Min(0.01f)] public float minimumStepSpeed = 0.08f;
        [Tooltip("Normalized run-cycle phase of the first contact; the other is half a cycle later.")]
        [Range(0f, 0.5f)] public float footstepPhaseOffset;
    }
}
