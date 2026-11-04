using UnityEngine;

namespace IntercomMvp
{
    [CreateAssetMenu(menuName = "Intercom MVP/Weapon Pose Profile", fileName = "WeaponPoseProfile")]
    public sealed class IntercomWeaponPoseProfile : ScriptableObject
    {
        [Tooltip("Optional override of IntercomCombat.controller for another weapon. Keep the same states and Arms mask.")]
        public AnimatorOverrideController animatorOverride;
        [Header("Clips used for timing (match the controller or its overrides)")]
        public AnimationClip raiseWeapons;
        public AnimationClip lowerWeapons;
        public AnimationClip shoot;
        [Header("Timing (seconds)")]
        [Tooltip("Legacy fallback only. With a Weapon Definition assigned, its Fire Interval controls firing.")]
        [Min(0.01f)] public float fireInterval = 0.18f;
        [Min(0f)] public float combatHold = 2f;
        [Range(0f, 0.2f)] public float poseBlend = 0.06f;
    }
}
