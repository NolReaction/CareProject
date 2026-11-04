using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using Unity.Profiling;
#endif

namespace IntercomMvp
{
    /// <summary>Editor/development frame capture. Release builds do no recording or IO.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(32000)]
    public sealed class IntercomPerformanceCapture : MonoBehaviour
    {
        public IntercomMissionDirector mission;
        public IntercomNpcNavigation navigation;
        public IntercomCombatEffects effects;
        public IntercomCameraOcclusion occlusion;
        public bool saveWhenStopped = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly IntercomFrameCapture capture = new IntercomFrameCapture(3600);
        private ProfilerRecorder mainThread, gcAllocated;
        private IntercomFrameCapture.Sample previous;
        private int previousCollections;
        private bool ready, focused = true, paused;
        public const string CaptureFileName = "CareProject_Performance_Latest.csv";
        public static string CapturePath => Path.Combine(Application.persistentDataPath, CaptureFileName);

        private void OnEnable()
        {
            capture.Clear(); ready = false;
            // Valid is checked when reading: unavailable counters are written as -1.
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            previousCollections = GC.CollectionCount(0);
        }
        private void LateUpdate()
        {
            if (!focused || paused || Time.timeScale <= 0f) { ready = false; return; }
            int collections = GC.CollectionCount(0);
            if (ready)
            {
                // Profiler LastValue is the completed PREVIOUS frame. Keep its
                // gameplay counters from that same frame, not the new spawn/kill.
                previous.wallMilliseconds = Time.unscaledDeltaTime * 1000.0;
                previous.mainThreadNanoseconds = mainThread.Valid ? mainThread.LastValue : -1;
                previous.gcAllocatedBytes = gcAllocated.Valid ? gcAllocated.LastValue : -1;
                previous.gcCollections = Math.Max(0, collections - previousCollections);
                capture.Add(previous);
            }
            previous = new IntercomFrameCapture.Sample {
                frame = Time.frameCount,
                alive = mission != null ? mission.AliveEnemies : -1,
                spawned = mission != null ? mission.SpawnedEnemies : -1,
                kills = mission != null ? mission.Kills : -1,
                prepared = mission != null ? mission.PreparedEnemyCount : -1,
                navNodes = navigation != null ? navigation.ExpandedNodesThisFrame : -1,
                shells = effects != null ? effects.ActiveShellCount : -1,
                effects = effects != null ? effects.ActiveEffectCount : -1,
                occlusionRebuilds = occlusion != null ? occlusion.CacheRebuildCount : -1,
                geometryBuilds = occlusion != null ? occlusion.GeometryBuildCount : -1
            };
            previousCollections = collections; ready = true;
        }
        private void OnApplicationFocus(bool value) { focused = value; ready = false; }
        private void OnApplicationPause(bool value) { paused = value; ready = false; }
        private void OnDisable()
        {
            mainThread.Dispose(); gcAllocated.Dispose(); ready = false;
            if (saveWhenStopped && capture.Count > 0) ExportRecentFrames();
        }
        [ContextMenu("Export Last Minute Of Frames")]
        public void ExportRecentFrames()
        {
            if (capture.Count == 0) return;
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                using (var writer = new StreamWriter(CapturePath, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("frame,wall_ms,main_thread_ns,gc_alloc_bytes,gc0_collections,alive,spawned,kills,prepared,nav_nodes,shells,effects,occlusion_rebuilds,geometry_builds");
                    for (int i = 0; i < capture.Count; i++)
                    {
                        var s = capture.GetOldest(i);
                        writer.Write(s.frame); writer.Write(',');
                        writer.Write(s.wallMilliseconds.ToString("F3", CultureInfo.InvariantCulture)); writer.Write(',');
                        writer.Write(s.mainThreadNanoseconds); writer.Write(','); writer.Write(s.gcAllocatedBytes); writer.Write(',');
                        writer.Write(s.gcCollections); writer.Write(','); writer.Write(s.alive); writer.Write(',');
                        writer.Write(s.spawned); writer.Write(','); writer.Write(s.kills); writer.Write(',');
                        writer.Write(s.prepared); writer.Write(','); writer.Write(s.navNodes); writer.Write(',');
                        writer.Write(s.shells); writer.Write(','); writer.Write(s.effects); writer.Write(',');
                        writer.Write(s.occlusionRebuilds); writer.Write(','); writer.WriteLine(s.geometryBuilds);
                    }
                }
                Debug.Log("CareProject: запись кадров сохранена: " + CapturePath, this);
            }
            catch (Exception error) { Debug.LogWarning("CareProject: не удалось сохранить кадры: " + error.Message, this); }
        }
#endif
    }
}
