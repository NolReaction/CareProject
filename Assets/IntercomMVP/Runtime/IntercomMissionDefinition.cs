using UnityEngine;

namespace IntercomMvp
{
    [CreateAssetMenu(menuName = "Intercom/Mission Definition", fileName = "Mission")]
    public sealed class IntercomMissionDefinition : ScriptableObject
    {
        public IntercomMissionMode mode;
        public string displayName = "Уничтожить 50 роботов";
        [Min(1)] public int killTarget = 50;
        [Range(1, 8)] public int maximumAlive = 6;
        [Min(1)] public int waveSize = 10;
        [Min(.1f)] public float spawnInterval = 1.2f;
        [Min(0f)] public float waveRestSeconds = 2.5f;
        [Min(1f)] public float minimumSpawnDistance = 5f;
        [Header("Rescue")]
        [Min(1)] public int rescueEnemySupply = 30;
        [Min(1f)] public float allyHealth = 60f;
        [Min(.1f)] public float allyMoveSpeed = 3.4f;
        [Min(.5f)] public float recruitDistance = 1.6f;
        [Min(.1f)] public float recruitSeconds = .6f;
        [Min(.5f)] public float extractionRadius = 2f;
        [Min(1f)] public float allyWaitDistance = 8f;
    }
}
