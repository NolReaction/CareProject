using UnityEngine;

namespace IntercomMvp
{
    /// <summary>One enemy archetype. Variants can share logic while changing these authored values.</summary>
    [CreateAssetMenu(menuName = "Intercom/Enemy Definition", fileName = "Udarnik")]
    public sealed class IntercomNpcDefinition : ScriptableObject
    {
        [Header("Health and punch")]
        [Min(1f)] public float maximumHealth = 75f;
        [Min(0f)] public float damage = 12f;
        [Min(.3f)] public float attackRange = 1.05f;
        [Range(5f, 85f)] public float attackHalfAngle = 40f;
        [Min(.1f)] public float windupSeconds = .30f;
        [Min(.1f)] public float recoverySeconds = .48f;
        [Header("Close attack tracking")]
        [Min(.03f)] public float attackCommitSeconds = .08f;
        [Min(1f)] public float attackTrackingTurnSpeed = 900f;
        [Header("Movement")]
        [Min(.1f)] public float moveSpeed = 2.65f;
        [Min(0f)] public float patrolSpeed = .9f;
        [Min(.1f)] public float acceleration = 10f;
        [Min(1f)] public float turnSpeed = 420f;
        [Min(.1f)] public float repathSeconds = .32f;
        [Range(.1f, 2f)] public float separationStrength = .9f;
        [Min(.1f)] public float stuckRecoverySeconds = .4f;
        [Header("Awareness")]
        [Min(1f)] public float detectionRadius = 10f;
        [Min(.1f)] public float loseTargetSeconds = 2.5f;
        [Min(1f)] public float leashRadius = 18f;
        [Min(0f)] public float patrolRadius = 1.5f;
        [Min(.1f)] public float idleSeconds = 1.1f;
        [Header("Shared presentation assets")]
        [Min(.3f)] public float emergenceSeconds = .68f;
        [Min(.5f)] public float emergenceDepth = 1.48f;
        public Material bodyMaterial;
        public Material detailMaterial;
        public Material telegraphMaterial;
        public Color bodyColor = new Color(.68f, .53f, .28f, 1f);

        public IntercomNpcRules Rules => new IntercomNpcRules(
            windupSeconds, recoverySeconds, loseTargetSeconds, idleSeconds);
        public static float Safe(float value, float fallback, float minimum, float maximum)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);
    }
}
