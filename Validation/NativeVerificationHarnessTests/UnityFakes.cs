// Compilation and safety-lifecycle substitutes. These do not execute Unity, PhysX, Animator, or a Profiler.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
namespace UnityEngine
{
 public class Object
 {
  public string name;public HideFlags hideFlags;public static readonly Dictionary<Type,Object> Found=new Dictionary<Type,Object>();
  public static T FindFirstObjectByType<T>() where T:Object =>Found.TryGetValue(typeof(T),out var x)?(T)x:null;
  public static T Instantiate<T>(T x) where T:Object =>(T)typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(x,null);
  public static void Destroy(Object x){}
 }
 public enum HideFlags { DontSave }
 public class ScriptableObject:Object{}
 public class Component:Object
 {
  public GameObject gameObject;public Transform transform=>gameObject.transform;
  public T GetComponent<T>() where T:class =>gameObject.GetComponent<T>();
  public T GetComponentInChildren<T>(bool includeInactive=false) where T:class =>gameObject.GetComponent<T>();
 }
 public class MonoBehaviour:Component{}
 public class GameObject:Object
 {
  public Transform transform=new Transform();private readonly List<Component> components=new List<Component>();
  public GameObject(){transform.gameObject=this;}
  public T AddComponent<T>() where T:Component,new(){var c=new T();c.gameObject=this;components.Add(c);return c;}
  public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault();
 }
 public class Transform:Component {public Vector3 position,localPosition;public Quaternion rotation,localRotation;}
 public class CharacterController:Component {public bool enabled=true;}
 public class Renderer:Component{}
 public class SkinnedMeshRenderer:Renderer {public Transform[] bones=new Transform[14];}
 public class Animator:Component {public Object runtimeAnimatorController=new Object();}
 public struct Vector3
 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 forward=>new Vector3(0,0,1);public static Vector3 back=>new Vector3(0,0,-1);public static Vector3 up=>new Vector3(0,1,0);public static Vector3 right=>new Vector3(1,0,0);public static Vector3 left=>new Vector3(-1,0,0);
  public float sqrMagnitude=>x*x+y*y+z*z;public Vector3 normalized=>this*(1/MathF.Sqrt(sqrMagnitude));
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
 }
 public struct Quaternion {public static Quaternion Euler(float x,float y,float z)=>default;public static Quaternion LookRotation(Vector3 x)=>default;public static Quaternion operator*(Quaternion a,Quaternion b)=>default;public static float Angle(Quaternion a,Quaternion b)=>0;}
 public static class Mathf {public static float Abs(float x)=>MathF.Abs(x);}
 public static class Time {public static float timeScale=1,unscaledTime;}
 public enum LogType { Error,Assert,Warning,Log,Exception }
 public static class Application
 {
  public static string persistentDataPath,unityVersion="Fake API - not Unity";public static bool runInBackground;public static string platform="API substitutes";
  public static event Action<string,string,LogType> logMessageReceived;
  public static void EmitLog(string s,LogType type)=>logMessageReceived?.Invoke(s,"fake stack",type);
 }
 public static class Debug
 {public static readonly List<string> Logs=new List<string>();public static void Log(string x)=>Logs.Add(x);public static void LogError(string x)=>Logs.Add(x);public static void LogWarning(string x)=>Logs.Add(x);}
 public static class JsonUtility
 {
  static readonly JsonSerializerOptions Options=new JsonSerializerOptions{IncludeFields=true};
  static readonly Dictionary<string,object> Saved=new Dictionary<string,object>();
  public static string ToJson(object x,bool pretty=false){var s=JsonSerializer.Serialize(x,x.GetType(),Options);Saved[s]=x;return s;}
  public static T FromJson<T>(string s)=>Saved.TryGetValue(s,out var x)?(T)x:JsonSerializer.Deserialize<T>(s,Options);
 }
}
namespace UnityEngine.SceneManagement
{
 public struct Scene
 {
  public string path;public bool isDirty,isLoaded;
  public static bool operator==(Scene a,Scene b)=>a.path==b.path;public static bool operator!=(Scene a,Scene b)=>!(a==b);
  public override bool Equals(object o)=>o is Scene s&&this==s;public override int GetHashCode()=>path?.GetHashCode()??0;
 }
 public static class SceneManager
 {
  public static Scene Active;public static readonly List<Scene> Scenes=new List<Scene>();
  public static int sceneCount=>Scenes.Count;public static Scene GetSceneAt(int i)=>Scenes[i];public static Scene GetActiveScene()=>Active;
 }
}
namespace UnityEditor
{
 public class InitializeOnLoadAttribute:Attribute{}
 public class MenuItemAttribute:Attribute {public MenuItemAttribute(string s){}}
 public enum PlayModeStateChange { EnteredPlayMode,ExitingPlayMode,EnteredEditMode,ExitingEditMode }
 public static class EditorApplication
 {
  public static bool isPlaying,isCompiling,isUpdating;public static bool isPlayingOrWillChangePlaymode=>isPlaying;
  public static double timeSinceStartup;public static int ExitCode=-1;
  public static event Action<PlayModeStateChange> playModeStateChanged;public static event Action update;public static event Action delayCall;
  public static void Raise(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode)isPlaying=true;if(s==PlayModeStateChange.EnteredEditMode)isPlaying=false;playModeStateChanged?.Invoke(s);}
  public static void Step(double delta=.1){timeSinceStartup+=delta;UnityEngine.Time.unscaledTime+=(float)delta;update?.Invoke();}
  public static void Exit(int code){ExitCode=code;}
 }
 public static class AssemblyReloadEvents {public static event Action beforeAssemblyReload;public static void Raise()=>beforeAssemblyReload?.Invoke();}
 public static class SessionState
 {
  public static readonly Dictionary<string,object> Data=new Dictionary<string,object>();
  public static bool GetBool(string k,bool v)=>Data.TryGetValue(k,out var x)?(bool)x:v;public static void SetBool(string k,bool v)=>Data[k]=v;
  public static int GetInt(string k,int v)=>Data.TryGetValue(k,out var x)?(int)x:v;public static void SetInt(string k,int v)=>Data[k]=v;
  public static float GetFloat(string k,float v)=>Data.TryGetValue(k,out var x)?(float)x:v;public static void SetFloat(string k,float v)=>Data[k]=v;
  public static string GetString(string k,string v)=>Data.TryGetValue(k,out var x)?(string)x:v;public static void SetString(string k,string v)=>Data[k]=v;
 }
 public class SceneAsset:UnityEngine.Object{}
 public static class AssetDatabase {public static bool Ready=true;public static T LoadAssetAtPath<T>(string s) where T:class =>Ready?new SceneAsset() as T:null;}
 public static class EditorUtility {public static void RevealInFinder(string s){}public static bool DisplayDialog(string a,string b,string c)=>true;}
}
namespace UnityEditor.SceneManagement
{
 public enum OpenSceneMode { Single }
 public struct SceneSetup {public string path;public bool isLoaded,isActive;}
 public static class EditorSceneManager
 {
  public static SceneSetup[] Restored;
  public static UnityEngine.SceneManagement.Scene OpenScene(string p,OpenSceneMode m)
  {
   var scene=new UnityEngine.SceneManagement.Scene{path=p,isLoaded=true};UnityEngine.SceneManagement.SceneManager.Scenes.Clear();UnityEngine.SceneManagement.SceneManager.Scenes.Add(scene);UnityEngine.SceneManagement.SceneManager.Active=scene;return scene;
  }
  public static void RestoreSceneManagerSetup(SceneSetup[] s){Restored=s;UnityEngine.SceneManagement.SceneManager.Scenes.Clear();foreach(var p in s)UnityEngine.SceneManagement.SceneManager.Scenes.Add(new UnityEngine.SceneManagement.Scene{path=p.path,isLoaded=p.isLoaded});}
 }
}
namespace IntercomMvp
{
 using UnityEngine;
 public enum IntercomMissionPhase{Selection,Active,Victory,Defeat}public enum IntercomMissionMode{Elimination,Rescue,FreePlay}
 public enum IntercomEnemySpawnKind{Perimeter,Ground,Hatch}public enum IntercomNpcPhase{Idle,Windup,Strike,Recover}public enum IntercomNavResult{Success,Deferred,Unreachable}
 public readonly struct IntercomDamage
 {public readonly float Amount;public readonly GameObject Instigator;public IntercomDamage(float a,Vector3 p,Vector3 d,GameObject g){Amount=a;Instigator=g;}}
 public readonly struct IntercomProjectileImpact {public readonly bool TargetHit,RobotHit;public IntercomProjectileImpact(bool t,bool r){TargetHit=t;RobotHit=r;}}
 public class IntercomHealth:Component
 {
  public float Maximum=100,Current=100;public bool invulnerable;public bool IsDead=>Current<=0;public event Action<IntercomDamage> Damaged;
  public bool TakeDamage(IntercomDamage d){if(invulnerable||Time.timeScale<=0||IsDead)return false;Current=Math.Max(0,Current-d.Amount);Damaged?.Invoke(d);return true;}
 }
 public class IntercomMissionDefinition:ScriptableObject {public int killTarget=50,maximumAlive=6,waveSize=10,visibleSpawnEvery=3,rescueEnemySupply=30;public bool useRhythmicWaves;public float spawnInterval=1.2f,waveRestSeconds=2.5f,minimumVisibleSpawnDistance=5.5f,minimumSpawnDistance=5;}
 public class IntercomEnemySpawnPoint:Component {public IntercomEnemySpawnKind kind;public float minimumDistance=5.5f;}
 public class IntercomNpcDefinition:ScriptableObject {public GameObject visualPrefab=new GameObject();}
 public class IntercomRobotDeathEffects:Component {public int ActivePieces,TotalBursts;}
 public class IntercomNpcNavigation:Component
 {
  public bool SegmentClear(Vector3 a,Vector3 b)=>true;public bool TryProject(Vector3 a,out Vector3 b){b=a;return true;}
  public IntercomNavResult TryFindPath(Vector3 a,Vector3 b,List<Vector3> p){p.Add(b);return IntercomNavResult.Success;}
 }
 public class IntercomMissionDirector:Component
 {
  public IntercomHealth playerHealth;public IntercomPlayerMotor playerMotor;public IntercomNpcNavigation navigation;public IntercomNpcDefinition enemyDefinition=new IntercomNpcDefinition();
  public IntercomMissionDefinition eliminationDefinition=new IntercomMissionDefinition(),rescueDefinition=new IntercomMissionDefinition();public Transform extractionPoint=new Transform();public IntercomRobotDeathEffects deathEffects;
  public Transform[] spawnPoints=Array.Empty<Transform>();public IntercomEnemySpawnPoint[] authoredSpawnPoints=Array.Empty<IntercomEnemySpawnPoint>();
  public IntercomMissionPhase Phase;public IntercomMissionMode Mode;public uint RunRevision;public float ElapsedSeconds;public int PreparedEnemyCount=8,AliveEnemies,SpawnedEnemies,Kills;public string ResultText="";
  private bool controlLocked,shooterWasEnabled;
  public bool StartMission(IntercomMissionMode m){if(controlLocked){playerMotor.GetComponent<IntercomSimpleShooter>().enabled=shooterWasEnabled;controlLocked=false;}Mode=m;Phase=IntercomMissionPhase.Active;RunRevision++;Kills=0;playerHealth.Current=100;return true;}
  public bool RetryMission()=>StartMission(Mode);public void ReturnToSelection(){if(!controlLocked){shooterWasEnabled=playerMotor.GetComponent<IntercomSimpleShooter>().enabled;controlLocked=true;}playerMotor.GetComponent<IntercomSimpleShooter>().enabled=false;Phase=IntercomMissionPhase.Selection;AliveEnemies=0;IntercomNpcController.Active.Clear();}
 }
 public class IntercomPlayerMotor:Component {public float modelForwardYaw=180;}
 public class IntercomPlayerInput:Component {public bool GameplayPaused;public bool Pause(){GameplayPaused=true;Time.timeScale=0;return true;}public void Resume(){GameplayPaused=false;Time.timeScale=1;}}
 public class IntercomWeaponPose:Component {public bool Held;public void SetTriggerHeld(bool x)=>Held=x;}
 public class IntercomWeaponDefinition:ScriptableObject {public float projectileLifetime=1.4f;}
 public class IntercomSimpleShooter:Component {public bool enabled=true;public IntercomWeaponDefinition Weapon;public float projectileLifetime=1.4f;public event Action<Vector3,int> VolleyFired;public event Action<IntercomProjectileImpact> DetailedProjectileHit;}
 public class IntercomProgression:Component {public bool IsDiagnosticSession;public int Begins,Ends;public bool BeginDiagnosticSession(){Begins++;IsDiagnosticSession=true;return true;}public void EndDiagnosticSession(){Ends++;IsDiagnosticSession=false;}}
 public class IntercomPerformanceCapture:Component
 {
  public IntercomMissionDirector mission;public IntercomNpcNavigation navigation;public bool saveWhenStopped=true;
  public bool TryGetMostRecentSample(out IntercomFrameCapture.Sample s){s=default;return false;}
  public void SuppressNextStopExport(){}
  public bool ExportRecentFramesTo(string p){Directory.CreateDirectory(Path.GetDirectoryName(p));File.WriteAllText(p,"fake test capture");return true;}
 }
 public class IntercomNpcController:Component
 {
  public static List<IntercomNpcController> Active=new List<IntercomNpcController>();public static int ActiveActorCount=>Active.Count;
  public static IntercomNpcController GetActiveActor(int i)=>i>=0&&i<Active.Count?Active[i]:null;
  public IntercomHealth Health;public uint LifeVersion,ResolvedStrikeVersion;public bool IsEmerging;public IntercomEnemySpawnKind EmergenceKind;public Renderer bodyRenderer;public IntercomNpcPhase Phase;
 }
 public class IntercomRescueAlly:Component {public bool Recruited;}
}
namespace IntercomMvp.Editor
{
 public static class IntercomProjectPaths{public static class TutorialScene{public static string Resolve()=>"Assets/Tutorial.unity";}}
 public static class IntercomProjectStartup{public static bool IsPristineDefaultScene(UnityEngine.SceneManagement.Scene s)=>s.isLoaded&&!s.isDirty&&string.IsNullOrEmpty(s.path);}
}
