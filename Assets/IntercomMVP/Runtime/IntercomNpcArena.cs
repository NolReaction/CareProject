using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Small authored NPC sandbox. Spawns once; combat reset belongs to the player's respawn epoch.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomNpcArena : MonoBehaviour
    {
        public IntercomNpcDefinition definition;
        public IntercomNpcNavigation navigation;
        public IntercomHealth playerHealth;
        public IntercomPlayerMotor playerMotor;
        public Transform[] spawnPoints;
        [Range(1, 16)] public int maximumActors = 3;

        private IntercomNpcController[] actors;
        private bool started;
        public int SpawnedCount { get; private set; }

        private void Start()
        {
            started = true;
            if (definition == null || navigation == null || playerHealth == null
                || playerMotor == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("NPC sandbox is missing its profile, navigation, player or spawn markers.", this);
                return;
            }
            if (!navigation.IsReady) navigation.RebuildObstacleCache();
            int count = Mathf.Min(Mathf.Clamp(maximumActors, 1, 16), spawnPoints.Length);
            actors = new IntercomNpcController[count];
            for (int i = 0; i < count; i++)
            {
                if (spawnPoints[i] == null || !navigation.TryProject(spawnPoints[i].position, out Vector3 point))
                {
                    Debug.LogWarning("NPC spawn marker has no reachable floor cell: " + (i + 1), this);
                    continue;
                }
                IntercomNpcController actor = IntercomNpcController.Create(definition, navigation,
                    playerHealth, playerMotor, point, i);
                if (actor == null) continue;
                actor.transform.SetParent(transform, true);
                actors[i] = actor;
                SpawnedCount++;
            }
        }

        private void OnEnable()
        {
            if (!started || actors == null) return;
            for (int i = 0; i < actors.Length; i++)
                if (actors[i] != null) actors[i].gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (actors == null) return;
            for (int i = 0; i < actors.Length; i++)
                if (actors[i] != null) actors[i].gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (actors == null) return;
            // Also handles removing just this component during Play. Only owned actors are touched.
            for (int i = 0; i < actors.Length; i++)
                if (actors[i] != null) Destroy(actors[i].gameObject);
            actors = null;
            SpawnedCount = 0;
        }
    }
}
