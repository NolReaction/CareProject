// Focused editor API stand-ins. Tests execute the production native authoring/migration code;
// these do not run Unity's FBX importer, prefab serialization, or an Animator.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using N=System.Numerics;
namespace UnityEngine
{
 public class Object { public string name; public static void DestroyImmediate(Object o){if(o is Component c)c.gameObject.components.Remove(c);} }
 public class ScriptableObject:Object {public static T CreateInstance<T>() where T:ScriptableObject,new()=>new T();}
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>()=>gameObject.GetComponent<T>();}
 public class Behaviour:Component{}
 public class MonoBehaviour:Behaviour{}
 public class GameObject:Object
 {
  public readonly List<Component> components=new List<Component>();public Transform transform;public GameObject source;
  public GameObject(string n=""){name=n;transform=new Transform{gameObject=this};components.Add(transform);}
  public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);return c;}
  public T GetComponent<T>()=>components.OfType<T>().FirstOrDefault();
  public T[] GetComponentsInChildren<T>(bool includeInactive=false)=>components.OfType<T>().Concat(transform.children.SelectMany(c=>c.gameObject.GetComponentsInChildren<T>(includeInactive))).ToArray();
  public T GetComponentInChildren<T>(bool includeInactive=false)=>GetComponentsInChildren<T>(includeInactive).FirstOrDefault();
 }
 public class Transform:Component
 {
  public Transform parent;public List<Transform> children=new List<Transform>();public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;
  public void SetParent(Transform p,bool worldPositionStays){parent?.children.Remove(this);parent=p;p?.children.Add(this);}
  public void SetPositionAndRotation(Vector3 p,Quaternion q){localPosition=p;localRotation=q;}
  public Transform Find(string n)=>children.FirstOrDefault(t=>t.gameObject.name==n);
  public Matrix4x4 localToWorldMatrix=>(parent==null?Matrix4x4.identity:parent.localToWorldMatrix)*Matrix4x4.TRS(localPosition,localRotation,localScale);
  public Matrix4x4 worldToLocalMatrix=>localToWorldMatrix.inverse;
 }
 public struct Vector3
 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public Vector3(float x,float y):this(x,y,0){}public static Vector3 zero=>default;public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);
  public static Vector3 operator*(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
 }
 public struct Quaternion {public N.Quaternion n;public static Quaternion identity=>new Quaternion{n=N.Quaternion.Identity};public static Quaternion Euler(float x,float y,float z)=>new Quaternion{n=N.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180)};}
 public struct Matrix4x4
 {
  N.Matrix4x4 n;public static Matrix4x4 identity=>new Matrix4x4{n=N.Matrix4x4.Identity};
  public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new Matrix4x4{n=N.Matrix4x4.CreateScale(s.x,s.y,s.z)*N.Matrix4x4.CreateFromQuaternion(q.n)*N.Matrix4x4.CreateTranslation(p.x,p.y,p.z)};
  public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new Matrix4x4{n=b.n*a.n};
  public Matrix4x4 inverse{get{N.Matrix4x4.Invert(n,out var inv);return new Matrix4x4{n=inv};}}
  public Vector3 GetColumn(int i)=>new Vector3(n.M41,n.M42,n.M43);
  public Quaternion rotation{get{N.Matrix4x4.Decompose(n,out var s,out var q,out var p);return new Quaternion{n=q};}}
  public Vector3 lossyScale{get{N.Matrix4x4.Decompose(n,out var s,out var q,out var p);return new Vector3(s.X,s.Y,s.Z);}}
 }
 public class RuntimeAnimatorController:Object{}
 public class Animator:Behaviour {public RuntimeAnimatorController runtimeAnimatorController;public bool applyRootMotion;public AnimatorCullingMode cullingMode;}
 public enum AnimatorCullingMode{AlwaysAnimate}
 public class Motion:Object{}
 public class AnimationClip:Motion{}
 public class Collider:Component{}
 public class Renderer:Component {public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;}
 public class MeshRenderer:Renderer{}
 public class SkinnedMeshRenderer:Renderer {public bool updateWhenOffscreen;}
 public class MeshFilter:Component {public Mesh sharedMesh;}
 public class Mesh:Object{}
 public class Texture2D:Object{}
 public class Shader:Object {public static Shader Find(string n)=>new Shader{name=n};}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);}
 public class Material:Object
 {public bool enableInstancing;public int renderQueue;public Material(Shader s){}public bool HasProperty(string n)=>true;public void SetTexture(string n,Texture2D t){}public void SetColor(string n,Color c){}public void SetFloat(string n,float f){}public void EnableKeyword(string n){}public void SetOverrideTag(string a,string b){}}
 public static class Application {public static string dataPath;}
 public static class Debug {public static List<string> Warnings=new List<string>();public static void LogWarning(string n)=>Warnings.Add(n);}
}
namespace UnityEngine.Rendering {public enum ShadowCastingMode{Off,On}public enum BlendMode{One,SrcAlpha,OneMinusSrcAlpha}public enum RenderQueue{Transparent=3000}}
namespace UnityEngine.SceneManagement
{
 public class Scene {public string path;public List<UnityEngine.GameObject> roots=new List<UnityEngine.GameObject>();public UnityEngine.GameObject[] GetRootGameObjects()=>roots.ToArray();}
 public static class SceneManager {public static Scene Active=new Scene();public static Scene GetActiveScene()=>Active;public static void MoveGameObjectToScene(UnityEngine.GameObject g,Scene s)=>s.roots.Add(g);}
}
namespace UnityEditor
{
 using UnityEngine;
 public class InitializeOnLoadMethodAttribute:Attribute{}
 public class MenuItem:Attribute{public MenuItem(string n,bool validate=false,int p=0){}}
 public static class EditorApplication {public static bool isCompiling,isUpdating,isPlayingOrWillChangePlaymode;public static Action delayCall;}
 public static class SessionState
 {static Dictionary<string,bool> b=new Dictionary<string,bool>();static Dictionary<string,int> i=new Dictionary<string,int>();public static bool GetBool(string k,bool d)=>b.TryGetValue(k,out var v)?v:d;public static void SetBool(string k,bool v)=>b[k]=v;public static int GetInt(string k,int d)=>i.TryGetValue(k,out var v)?v:d;public static void SetInt(string k,int v)=>i[k]=v;public static void Clear(){b.Clear();i.Clear();}}
 public static class Undo
 {public static List<Object> Recorded=new List<Object>();public static void RecordObject(Object o,string n)=>Recorded.Add(o);public static void RegisterCreatedObjectUndo(Object o,string n){}public static T AddComponent<T>(GameObject o) where T:Component,new()=>o.AddComponent<T>();}
 public static class EditorUtility {public static void SetDirty(Object o){}}
 public class AssetImporter:Object
 {public static Dictionary<string,AssetImporter> importers=new Dictionary<string,AssetImporter>();public static AssetImporter GetAtPath(string p)=>importers.TryGetValue(p,out var i)?i:null;}
 public enum ModelImporterAnimationType{None,Generic}
 public enum ModelImporterAvatarSetup{NoAvatar,CreateFromThisModel}
 public class ModelImporter:AssetImporter {public bool importAnimation;public ModelImporterAnimationType animationType;public ModelImporterAvatarSetup avatarSetup;public int Reimports;public void SaveAndReimport(){Reimports++;}}
 public static class AssetDatabase
 {
  public static Dictionary<string,Object> Assets=new Dictionary<string,Object>();public static Dictionary<Object,string> Paths=new Dictionary<Object,string>();public static Dictionary<string,Object[]> SubAssets=new Dictionary<string,Object[]>();static Dictionary<Object,string[]> labels=new Dictionary<Object,string[]>();
  public static int LoadAllCalls,SaveCalls,CreateCalls;public static T LoadAssetAtPath<T>(string p) where T:Object=>Assets.TryGetValue(p,out var o)?o as T:null;
  public static Object[] LoadAllAssetsAtPath(string p){LoadAllCalls++;return SubAssets.TryGetValue(p,out var a)?a:Array.Empty<Object>();}
  public static string GetAssetPath(Object o)=>o!=null&&Paths.TryGetValue(o,out var p)?p:"";
  public static void Register(Object o,string p,bool disk=true){Assets[p]=o;Paths[o]=p;if(disk){string f=Path.Combine(Path.GetDirectoryName(Application.dataPath),p);Directory.CreateDirectory(Path.GetDirectoryName(f));File.WriteAllText(f,"original "+Path.GetFileName(p));File.WriteAllText(f+".meta","guid "+p);}}
  public static void CreateAsset(Object o,string p){CreateCalls++;Register(o,p);}
  public static string[] GetLabels(Object o)=>labels.TryGetValue(o,out var a)?a:Array.Empty<string>();public static void SetLabels(Object o,string[] a)=>labels[o]=a;
  public static void SaveAssets()=>SaveCalls++;
  public static void Clear(){Assets.Clear();Paths.Clear();SubAssets.Clear();labels.Clear();LoadAllCalls=SaveCalls=CreateCalls=0;AssetImporter.importers.Clear();}
 }
 public static class PrefabUtility
 {
  public static Dictionary<string,int> SaveCounts=new Dictionary<string,int>();
  public static GameObject GetCorrespondingObjectFromOriginalSource(GameObject g)=>g.source;
  public static Object InstantiatePrefab(GameObject source,UnityEngine.SceneManagement.Scene s){var g=Clone(source);g.source=source.source??source;s.roots.Add(g);return g;}
  static GameObject Clone(GameObject source)
  {
   var g=new GameObject(source.name);g.transform.localPosition=source.transform.localPosition;g.transform.localRotation=source.transform.localRotation;g.transform.localScale=source.transform.localScale;
   foreach(var c in source.components){if(c is Transform)continue;var copy=(Component)Activator.CreateInstance(c.GetType());copy.gameObject=g;foreach(var f in c.GetType().GetFields())if(f.Name!="gameObject")f.SetValue(copy,f.GetValue(c));g.components.Add(copy);}
   foreach(var t in source.transform.children){var copy=Clone(t.gameObject);copy.transform.SetParent(g.transform,false);}return g;
  }
  public static GameObject SaveAsPrefabAsset(GameObject g,string p){SaveCounts[p]=SaveCounts.TryGetValue(p,out var c)?c+1:1;AssetDatabase.Register(g,p);return g;}
 }
}
namespace UnityEditor.SceneManagement
{
 public static class EditorSceneManager {public static int OpenPreviews,ClosedPreviews;public static UnityEngine.SceneManagement.Scene NewPreviewScene(){OpenPreviews++;return new UnityEngine.SceneManagement.Scene();}public static void ClosePreviewScene(UnityEngine.SceneManagement.Scene s){ClosedPreviews++;}public static void MarkSceneDirty(UnityEngine.SceneManagement.Scene s){}}
}
namespace UnityEditor.Animations
{
 using UnityEngine;
 public class AnimatorController:RuntimeAnimatorController
 {
  public AnimatorControllerLayer[] layers={new AnimatorControllerLayer()};public Object[] parameters=Array.Empty<Object>();
  public static AnimatorController CreateAnimatorControllerAtPath(string p){var c=new AnimatorController();UnityEditor.AssetDatabase.CreateAsset(c,p);return c;}
 }
 public class AnimatorControllerLayer {public string name="Base Layer";public Object avatarMask;public int syncedLayerIndex=-1;public AnimatorStateMachine stateMachine=new AnimatorStateMachine();}
 public struct ChildAnimatorState {public AnimatorState state;}
 public class AnimatorStateMachine:Object
 {
  public Object[] stateMachines=Array.Empty<Object>(),behaviours=Array.Empty<Object>(),anyStateTransitions=Array.Empty<Object>(),entryTransitions=Array.Empty<Object>();
  public ChildAnimatorState[] states=Array.Empty<ChildAnimatorState>();public AnimatorState defaultState;
  public AnimatorState AddState(string n,Vector3 pos){var s=new AnimatorState{name=n};states=states.Concat(new[]{new ChildAnimatorState{state=s}}).ToArray();return s;}
  public void RemoveState(AnimatorState s)=>states=states.Where(c=>c.state!=s).ToArray();
 }
 public class AnimatorState:Object
 {
  public Motion motion;public Object[] transitions=Array.Empty<Object>(),behaviours=Array.Empty<Object>();public bool writeDefaultValues=true,mirror,iKOnFeet,speedParameterActive,mirrorParameterActive,cycleOffsetParameterActive,timeParameterActive;public float speed=1,cycleOffset;public string tag;
 }
}
namespace IntercomMvp
{
 using UnityEngine;
 public enum IntercomTeam{Player}
 public enum IntercomEnemySpawnKind{Ground,Hatch}
 public class IntercomNpcDefinition:ScriptableObject {public Material bodyMaterial,detailMaterial,telegraphMaterial;public GameObject visualPrefab,breakupPrefab;}
 public class IntercomEnemySpawnPoint:MonoBehaviour {public IntercomEnemySpawnKind kind;}
 public class IntercomMissionDirector:MonoBehaviour {public IntercomEnemySpawnPoint[] authoredSpawnPoints;}
 public class IntercomHealth:MonoBehaviour {public float maximumHealth,postHitInvulnerability;public IntercomTeam team;}
 public class IntercomPlayerMotor:MonoBehaviour{}
 public class IntercomPlayerInput:MonoBehaviour {public IntercomPlayerVitality vitality;}
 public class IntercomSimpleShooter:MonoBehaviour{}
 public class IntercomWeaponPose:MonoBehaviour{}
 public class IntercomPlayerVitality:MonoBehaviour {public IntercomHealth health;public IntercomPlayerMotor motor;public IntercomPlayerInput input;public IntercomSimpleShooter shooter;public IntercomWeaponPose weaponPose;}
 public class IntercomNpcNavigation:MonoBehaviour {public Transform obstacleRoot;}
 public class IntercomNpcArena:MonoBehaviour {public IntercomNpcDefinition definition;public IntercomNpcNavigation navigation;public IntercomHealth playerHealth;public IntercomPlayerMotor playerMotor;public Transform[] spawnPoints;public int maximumActors;}
}
namespace IntercomMvp.Editor
{
 public static class IntercomMvpSceneBuilder {public const string ScenePath="scene";}
 public static class IntercomDistrictSetup {public const string RootName="environment";}
 public static class IntercomAssetCatalog {public static void EnsureParentFolder(string n){}}
 public static class IntercomPlayerHurtSetup {public static void Setup(UnityEngine.GameObject g){}}
 public static class IntercomPerformanceTools {public static void Setup(UnityEngine.GameObject g,IntercomMvp.IntercomNpcNavigation n){}}
}
