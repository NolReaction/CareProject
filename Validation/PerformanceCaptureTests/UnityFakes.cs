using System;
namespace UnityEngine {
 public class MonoBehaviour {}
 public class DisallowMultipleComponent:Attribute{}
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int x){}}
 public class ContextMenu:Attribute {public ContextMenu(string x){}}
 public static class Time {public static int frameCount;public static float timeScale=1,unscaledDeltaTime=1f/60;}
 public static class Application {public static string persistentDataPath;}
 public static class Debug {public static int logs;public static void Log(string m,object c){logs++;}public static void LogWarning(string m,object c){throw new Exception(m);}}
}
namespace Unity.Profiling {
 public struct ProfilerCategory {internal bool memory;public static ProfilerCategory Internal=>default;public static ProfilerCategory Memory=>new ProfilerCategory{memory=true};}
 public struct ProfilerRecorder {
  bool memory;public static bool Supported=true;public static long Cpu=15000000,Gc=0;public static int disposed;
  public bool Valid=>Supported;public long LastValue=>memory?Gc:Cpu;
  public static ProfilerRecorder StartNew(ProfilerCategory category,string name,int capacity)=>new ProfilerRecorder{memory=category.memory};
  public void Dispose(){disposed++;}
 }
}
namespace IntercomMvp {
 public class IntercomMissionDirector {public int AliveEnemies,SpawnedEnemies,Kills,PreparedEnemyCount;}
 public class IntercomNpcNavigation {public int ExpandedNodesThisFrame;}
 public class IntercomCombatEffects {public int ActiveShellCount,ActiveEffectCount;}
 public class IntercomCameraOcclusion {public int CacheRebuildCount,GeometryBuildCount;}
}
