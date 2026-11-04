using System.Collections.Generic;
using UnityEngine;

namespace IntercomMvp
{
    public enum IntercomNavResult { Success, Unreachable, Deferred }

    /// <summary>One shared flat-yard grid; cached authored obstacles, bounded path requests and no NavMesh bake.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public sealed class IntercomNpcNavigation : MonoBehaviour
    {
        public Transform obstacleRoot;
        public Vector2 worldMin = new Vector2(-18f, -16f);
        public Vector2 worldSize = new Vector2(36f, 32f);
        [Range(.25f, 1f)] public float cellSize = .4f;
        [Min(.1f)] public float agentClearanceRadius = .4f;
        public float floorHeight;
        [Min(0f)] public float stepHeight = .18f;
        [Min(.5f)] public float agentHeight = 1.35f;
        [Min(.05f)] public float obstacleRefreshSeconds = .2f;
        [Range(1, 8)] public int maximumSearchesPerFrame = 2;
        // A finished search may exceed this soft budget. Later queries wait
        // until the next frame instead of stacking several costly A* searches.
        [Min(64)] public int maximumExpandedNodesPerFrame = 1200;
        [Min(0f)] public float maximumGoalSnap = 1f;

        public uint Version => revision;
        public bool IsReady => grid != null && cachedRoot != null;
        public int LastExpandedNodes => grid != null ? grid.LastExpandedNodes : 0;
        public int ExpandedNodesThisFrame => searchFrame==Time.frameCount ? expandedNodes : 0;
        private IntercomNavGrid grid;
        private Transform cachedRoot;
        private Collider[] colliders;
        private Bounds[] oldBounds;
        private bool[] oldBlocking;
        private List<IntercomNavPoint> path;
        private uint revision;
        private float nextRefresh;
        private int searchFrame = -1, searches, expandedNodes;

        private void Start() => RebuildObstacleCache();
        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (cachedRoot != obstacleRoot || grid == null) RebuildObstacleCache();
            if (Time.unscaledTime >= nextRefresh) RefreshObstacles(false);
        }

        [ContextMenu("Rebuild NPC Navigation")]
        public void RebuildObstacleCache()
        {
            cachedRoot = obstacleRoot;
            if (cachedRoot == null) { grid = null; colliders = null; return; }
            float step = Safe(cellSize, .4f, .25f, 1f);
            float width = Safe(worldSize.x, 36f, 1f, 64f), depth = Safe(worldSize.y, 32f, 1f, 64f);
            // Keep pathological Inspector values below the pure grid's fixed memory ceiling.
            if (width * depth / (step * step) > 32000f) step = Mathf.Max(step, .5f);
            float x = Finite(worldMin.x) ? worldMin.x : -18f, z = Finite(worldMin.y) ? worldMin.y : -16f;
            grid = new IntercomNavGrid(x, z, width, depth, step);
            path = new List<IntercomNavPoint>(grid.Count);
            colliders = cachedRoot.GetComponentsInChildren<Collider>(true);
            oldBounds = new Bounds[colliders.Length]; oldBlocking = new bool[colliders.Length];
            unchecked { revision++; }
            RefreshObstacles(true);
        }

        public IntercomNavResult TryFindPath(Vector3 start, Vector3 destination, List<Vector3> output)
        {
            if (output == null) throw new System.ArgumentNullException(nameof(output));
            if (!isActiveAndEnabled || Time.timeScale <= 0f) return IntercomNavResult.Deferred;
            if (cachedRoot != obstacleRoot || grid == null) RebuildObstacleCache();
            if (!IsReady) { output.Clear(); return IntercomNavResult.Unreachable; }
            if (Time.unscaledTime >= nextRefresh) RefreshObstacles(false);
            if (searchFrame != Time.frameCount) { searchFrame = Time.frameCount; searches = expandedNodes = 0; }
            if (searches >= Mathf.Clamp(maximumSearchesPerFrame, 1, 8)) return IntercomNavResult.Deferred;
            if (searches>0 && expandedNodes>=Mathf.Clamp(maximumExpandedNodesPerFrame,64,32768))
                return IntercomNavResult.Deferred;
            searches++;
            bool found=grid.FindPath(start.x, start.z, destination.x, destination.z,
                Safe(maximumGoalSnap, 1f, 0f, 2f), path);
            expandedNodes+=grid.LastExpandedNodes;
            if (!found)
            { output.Clear(); return IntercomNavResult.Unreachable; }
            output.Clear();
            if (output.Capacity < path.Count) output.Capacity = path.Count;
            float y = Finite(floorHeight) ? floorHeight : 0f;
            for (int i = 0; i < path.Count; i++) output.Add(new Vector3(path[i].X, y, path[i].Z));
            return IntercomNavResult.Success;
        }

        public bool TryProject(Vector3 position, out Vector3 projected)
        {
            projected = position;
            if (cachedRoot != obstacleRoot || grid == null) RebuildObstacleCache();
            if (!IsReady || !grid.TryProject(position.x, position.z, Safe(maximumGoalSnap, 1f, 0f, 2f), out IntercomNavPoint point)) return false;
            projected = new Vector3(point.X, Finite(floorHeight) ? floorHeight : 0f, point.Z);
            return true;
        }

        public bool SegmentClear(Vector3 start, Vector3 destination)
        {
            if (!IsReady) return false;
            return grid.SegmentClear(start.x, start.z, destination.x, destination.z);
        }

        /// <summary>Existing colliders may switch state or move; new hierarchy objects require explicit RebuildObstacleCache.</summary>
        private void RefreshObstacles(bool force)
        {
            if (!IsReady || colliders == null) return;
            nextRefresh = Time.unscaledTime + Safe(obstacleRefreshSeconds, .2f, .05f, 2f);
            bool changed = force;
            float floor = Finite(floorHeight) ? floorHeight : 0f;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider item = colliders[i];
                Bounds bounds = item != null ? item.bounds : default;
                bool blocking = item != null && !(item is CharacterController) && item.enabled
                    && item.gameObject.activeInHierarchy && !item.isTrigger
                    && bounds.max.y > floor + Safe(stepHeight, .18f, 0f, .4f)
                    && bounds.min.y < floor + Safe(agentHeight, 1.35f, .5f, 3f);
                if (blocking != oldBlocking[i] || (blocking && bounds != oldBounds[i])) changed = true;
                oldBlocking[i] = blocking; oldBounds[i] = bounds;
            }
            if (!changed) return;
            grid.BeginObstacles();
            float radius = Safe(agentClearanceRadius, .4f, .1f, 1.5f);
            for (int i = 0; i < colliders.Length; i++)
                if (oldBlocking[i])
                {
                    Bounds b = oldBounds[i];
                    grid.AddObstacle(b.min.x, b.min.z, b.max.x, b.max.z, radius);
                }
            if (grid.EndObstacles()) unchecked { revision++; }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Safe(float value, float fallback, float min, float max)
            => Finite(value) ? Mathf.Clamp(value, min, max) : fallback;
    }
}
