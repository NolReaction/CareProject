// Deliberately narrow engine stand-ins. This compiles the real GroundFeedback and Motor
// together, but does not claim Unity physics, mesh skinning or Animator playback.
using System;
using System.Collections.Generic;
using System.Linq;
using N=System.Numerics;
namespace UnityEngine {
 public class Object {public string name; public static void Destroy(Object o){} public static void DestroyImmediate(Object o){} }
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform; public T GetComponent<T>() where T:class=>gameObject.parts.OfType<T>().FirstOrDefault();public T GetComponentInParent<T>() where T:class{for(var p=transform;p!=null;p=p.parent){var x=p.GetComponent<T>();if(x!=null)return x;}return null;}public T GetComponentInChildren<T>(bool inactive=false) where T:class=>GetComponentsInChildren<T>(inactive).FirstOrDefault();public T[] GetComponentsInChildren<T>(bool inactive=false) where T:class=>gameObject.parts.Concat(gameObject.children).OfType<T>().ToArray();}
 public class Behaviour:Component {private bool flag=true;public bool enabled{get=>flag;set{if(flag==value)return;flag=value;if(gameObject!=null&&gameObject.activeInHierarchy)Lifecycle.Call(this,value?"OnEnable":"OnDisable");}}public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;}
 public class MonoBehaviour:Behaviour {}
 public class ScriptableObject:Object {}
 public class CreateAssetMenuAttribute:Attribute { public string menuName,fileName; }
 public class GameObject:Object {public static GameObject CreatePrimitive(PrimitiveType shape){var g=new GameObject();g.AddComponent<Collider>();g.AddComponent<Renderer>();return g;}public T GetComponent<T>() where T:class=>parts.OfType<T>().FirstOrDefault();public T GetComponentInParent<T>() where T:class=>transform.GetComponentInParent<T>();public bool activeSelf=true;public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy); public void SetActive(bool v){activeSelf=v;}public Transform transform;public List<object> parts=new List<object>(),children=new List<object>();public GameObject(string n=""){name=n;transform=new Transform{gameObject=this,name=n};parts.Add(transform);}public T AddComponent<T>() where T:Component,new(){var x=new T{gameObject=this};parts.Add(x);return x;}}
 public struct Matrix4x4 {
  internal N.Matrix4x4 value;public static Matrix4x4 identity=>new Matrix4x4{value=N.Matrix4x4.Identity};
  public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 scale)=>new Matrix4x4{value=N.Matrix4x4.CreateScale(scale.n)*N.Matrix4x4.CreateFromQuaternion(q.n)*N.Matrix4x4.CreateTranslation(p.n)};
  public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new Matrix4x4{value=b.value*a.value};
  public Vector3 MultiplyPoint3x4(Vector3 p)=>Vector3.V(N.Vector3.Transform(p.n,value));
  public Matrix4x4 inverse {get{N.Matrix4x4.Invert(value,out var result);return new Matrix4x4{value=result};}}
 }
 public class Transform:Component {
  public Transform parent;public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;
  public Vector3 position {get=>parent==null?localPosition:parent.TransformPoint(localPosition);set=>localPosition=parent==null?value:parent.InverseTransformPoint(value);}
  public Quaternion rotation {get=>parent==null?localRotation:parent.rotation*localRotation;set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value;}
  public Matrix4x4 localToWorldMatrix=>parent==null?Matrix4x4.TRS(localPosition,localRotation,localScale):parent.localToWorldMatrix*Matrix4x4.TRS(localPosition,localRotation,localScale);
  public Matrix4x4 worldToLocalMatrix=>localToWorldMatrix.inverse;
  public Vector3 lossyScale {get{var m=localToWorldMatrix.value;return new Vector3(new N.Vector3(m.M11,m.M12,m.M13).Length(),new N.Vector3(m.M21,m.M22,m.M23).Length(),new N.Vector3(m.M31,m.M32,m.M33).Length());}}
  public Vector3 forward=>rotation*Vector3.forward;
  public void SetParent(Transform t,bool stay){var p=position;var q=rotation;parent=t;if(stay){position=p;rotation=q;}}
  public bool IsChildOf(Transform t){for(var p=this;p!=null;p=p.parent)if(p==t)return true;return false;}
  public Vector3 TransformPoint(Vector3 v){v=localPosition+localRotation*new Vector3(v.x*localScale.x,v.y*localScale.y,v.z*localScale.z);return parent==null?v:parent.TransformPoint(v);}
  public Vector3 InverseTransformPoint(Vector3 v){if(parent!=null)v=parent.InverseTransformPoint(v);v=Quaternion.Inverse(localRotation)*(v-localPosition);return new Vector3(v.x/localScale.x,v.y/localScale.y,v.z/localScale.z);}
  public Vector3 TransformDirection(Vector3 v)=>rotation*v;
  public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}
 }

 public struct Vector3 {public void Normalize(){var v=normalized;x=v.x;y=v.y;z=v.z;}public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}internal N.Vector3 n=>new N.Vector3(x,y,z);internal static Vector3 V(N.Vector3 a)=>new Vector3(a.X,a.Y,a.Z);public static Vector3 zero=>new Vector3();public static Vector3 one=>new Vector3(1,1,1);public static Vector3 up=>new Vector3(0,1,0);public static Vector3 down=>-up;public static Vector3 forward=>new Vector3(0,0,1);public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude>1e-8f?this/magnitude:zero;public static Vector3 operator+(Vector3 a,Vector3 b)=>V(a.n+b.n);public static Vector3 operator-(Vector3 a,Vector3 b)=>V(a.n-b.n);public static Vector3 operator-(Vector3 a)=>V(-a.n);public static Vector3 operator*(Vector3 a,float b)=>V(a.n*b);public static Vector3 operator*(float b,Vector3 a)=>a*b;public static Vector3 operator/(Vector3 a,float b)=>V(a.n/b);public static float Dot(Vector3 a,Vector3 b)=>N.Vector3.Dot(a.n,b.n);public static Vector3 Cross(Vector3 a,Vector3 b)=>V(N.Vector3.Cross(a.n,b.n));public static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n)=>a-n*Dot(a,n);public static Vector3 Min(Vector3 a,Vector3 b)=>V(N.Vector3.Min(a.n,b.n));public static Vector3 Max(Vector3 a,Vector3 b)=>V(N.Vector3.Max(a.n,b.n));public static Vector3 ClampMagnitude(Vector3 a,float f)=>a.magnitude>f?a.normalized*f:a;public static Vector3 MoveTowards(Vector3 a,Vector3 b,float d)=>a+ClampMagnitude(b-a,d);public static float Angle(Vector3 a,Vector3 b)=>(float)(Math.Acos(Math.Clamp(Dot(a.normalized,b.normalized),-1,1))*180/Math.PI);public static float SignedAngle(Vector3 a,Vector3 b,Vector3 n)=>Angle(a,b)*Math.Sign(Dot(Cross(a,b),n));}
 public struct Vector2 {public float sqrMagnitude=>x*x+y*y;public void Normalize(){float m=magnitude;if(m>0){x/=m;y/=m;}} public static Vector2 operator+(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);public float x,y;public Vector2(float a,float b){x=a;y=b;}public float magnitude=>(float)Math.Sqrt(x*x+y*y);public static Vector2 zero=>new Vector2();public static Vector2 one=>new Vector2(1,1);public static Vector2 up=>new Vector2(0,1);public static Vector2 right=>new Vector2(1,0);public static Vector2 ClampMagnitude(Vector2 a,float f)=>a.magnitude>f?new Vector2(a.x/a.magnitude*f,a.y/a.magnitude*f):a;}
 public struct Quaternion {public float x,y,z,w;internal N.Quaternion n=>new N.Quaternion(x,y,z,w);internal static Quaternion Q(N.Quaternion n)=>new Quaternion{x=n.X,y=n.Y,z=n.Z,w=n.W};public static float Dot(Quaternion a,Quaternion b)=>N.Quaternion.Dot(a.n,b.n);public static Quaternion identity=>Q(N.Quaternion.Identity);public static Quaternion Inverse(Quaternion q)=>Q(N.Quaternion.Inverse(q.n));public static Quaternion operator*(Quaternion a,Quaternion b)=>Q(a.n*b.n);public static Vector3 operator*(Quaternion a,Vector3 b)=>Vector3.V(N.Vector3.Transform(b.n,a.n));public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>Q(N.Quaternion.Slerp(a.n,b.n,t));public static Quaternion AngleAxis(float deg,Vector3 axis)=>Q(N.Quaternion.CreateFromAxisAngle(axis.normalized.n,deg*(float)Math.PI/180));public static Quaternion Euler(float x,float y,float z)=>Q(N.Quaternion.CreateFromYawPitchRoll(y*(float)Math.PI/180,x*(float)Math.PI/180,z*(float)Math.PI/180));public static Quaternion FromToRotation(Vector3 a,Vector3 b){float d=Vector3.Dot(a.normalized,b.normalized);if(d>=1f)return identity;if(d<=-1f)return AngleAxis(180,Vector3.up);return Q(N.Quaternion.Normalize(new N.Quaternion(Vector3.Cross(a.normalized,b.normalized).n,1+d)));}public static Quaternion RotateTowards(Quaternion a,Quaternion b,float degrees){float d=Math.Abs(N.Quaternion.Dot(a.n,b.n));float angle=2*(float)Math.Acos(Math.Clamp(d,-1,1))*180/(float)Math.PI;return Slerp(a,b,angle<.00001f?1:Math.Min(1,degrees/angle));}public static Quaternion LookRotation(Vector3 f,Vector3 u)=>FromToRotation(Vector3.forward,f);public static Quaternion LookRotation(Vector3 f)=>LookRotation(f,Vector3.up);}
 public struct Color {public float r,g,b,a;public static Color white=>new Color(1,1,1,1);public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public class Material:Object {} public class MaterialPropertyBlock {public void Clear(){}public void SetColor(int id,Color c){}}
 public static class Shader {public static int PropertyToID(string s)=>s.GetHashCode();}
 public class Mesh:Object {public Vector3[] vertices=Array.Empty<Vector3>();public Vector2[] uv;public int[] triangles;public void RecalculateBounds(){}public void UploadMeshData(bool r){}}
 public class Renderer:Behaviour {public Material sharedMaterial;public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;public Rendering.LightProbeUsage lightProbeUsage;public Rendering.ReflectionProbeUsage reflectionProbeUsage;public void SetPropertyBlock(MaterialPropertyBlock p){}public void GetPropertyBlock(MaterialPropertyBlock p){}}
 public class MeshRenderer:Renderer {}public class MeshFilter:Component {public Mesh sharedMesh;}
 public class SkinnedMeshRenderer:Renderer {public Vector3[] vertices=Array.Empty<Vector3>();public int bakes;public bool fail;public void BakeMesh(Mesh mesh){bakes++;if(fail)throw new Exception("mesh bake failed");mesh.vertices=(Vector3[])vertices.Clone();}}
 public class RuntimeAnimatorController:Object {public AnimationClip[] animationClips=Array.Empty<AnimationClip>();}
 public class AnimationClip:Object {public Action<GameObject> sample;public int samples;public void SampleAnimation(GameObject target,float time){samples++;sample?.Invoke(target);}}
 public class Animator:Behaviour {public float speed=1;public bool applyRootMotion;public RuntimeAnimatorController runtimeAnimatorController;public static int StringToHash(string s)=>s.GetHashCode();public Dictionary<int,float> values=new Dictionary<int,float>();public float GetFloat(string s)=>GetFloat(StringToHash(s));public float GetFloat(int s)=>values.TryGetValue(s,out var v)?v:0;public void SetFloat(int s,float v){values[s]=v;}public void SetFloat(string s,float v){SetFloat(StringToHash(s),v);}public void SetFloat(int s,float v,float damping,float dt){SetFloat(s,v);}public AnimatorStateInfo state;public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer)=>state;}
 public struct AnimatorStateInfo {public float normalizedTime;public int fullPathHash;}
 public enum PrimitiveType{Cylinder}public class Collider:Component {public bool enabled=true,isTrigger;}public class CharacterController:Collider {public bool isGrounded=true;public float height=1.5f,radius=.3f,skinWidth=.03f;public Vector3 center;public CollisionFlags Move(Vector3 v){transform.position+=v;return CollisionFlags.None;}}
 [Flags]public enum CollisionFlags{None=0,Above=1}public struct RaycastHit{public Collider collider;public Vector3 point,normal;public float distance;}public enum QueryTriggerInteraction{Ignore}public struct LayerMask{public int value;public static implicit operator int(LayerMask m)=>m.value;public static implicit operator LayerMask(int i)=>new LayerMask{value=i};}
 public static class Physics {public static int SphereCastNonAlloc(Vector3 o,float r,Vector3 d,RaycastHit[] hits,float distance,int mask,QueryTriggerInteraction q)=>0;public static Collider Floor;public static float FloorHeight;public static Vector3 FloorNormal=Vector3.up;public static int RaycastNonAlloc(Vector3 a,Vector3 b,RaycastHit[] h,float d,int l,QueryTriggerInteraction q){float y=FloorHeight-(FloorNormal.x*a.x+FloorNormal.z*a.z)/FloorNormal.y;float distance=a.y-y;if(Floor==null||distance<0||distance>d)return 0;h[0]=new RaycastHit{collider=Floor,point=new Vector3(a.x,y,a.z),normal=FloorNormal,distance=distance};return 1;}}
 public class Camera:Behaviour {public Vector3 WorldToScreenPoint(Vector3 p)=>new Vector3(p.x,p.z,1);public static Camera main;public Ray ScreenPointToRay(Vector2 p)=>new Ray(new Vector3(p.x/100,10,p.y/100),new Vector3(0,-1,1));}
 public static class Time {public static float time,timeScale=1,unscaledTime;public static float deltaTime=1f/60;public static int frameCount;}
 public static class Application {public static bool isPlaying=true,isMobilePlatform;}
 public static class Debug {public static void LogWarning(string s,Object context){}}
 public static class Mathf {public static int Min(int a,int b)=>Math.Min(a,b);public static int Clamp(int a,int b,int c)=>Math.Clamp(a,b,c);public static int RoundToInt(float a)=>(int)Math.Round(a);public static int CeilToInt(float a)=>(int)Math.Ceiling(a);public static float Sqrt(float a)=>(float)Math.Sqrt(a);public static int Max(int a,int b)=>Math.Max(a,b);public static float Abs(float a)=>Math.Abs(a);public const float PI=(float)Math.PI;public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static float Clamp01(float x)=>Math.Clamp(x,0,1);public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);public static float Pow(float a,float b)=>(float)Math.Pow(a,b);public static float Sin(float x)=>(float)Math.Sin(x);public static float MoveTowards(float a,float b,float d)=>a<b?Math.Min(a+d,b):Math.Max(a-d,b);}
 public class DisallowMultipleComponent:Attribute{}public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int i){}}public class RequireComponent:Attribute{public RequireComponent(Type t){}}public class HeaderAttribute:Attribute{public HeaderAttribute(string s){}}public class TooltipAttribute:Attribute{public TooltipAttribute(string s){}}public class RangeAttribute:Attribute{public RangeAttribute(float a,float b){}}public class MinAttribute:Attribute{public MinAttribute(float a){}}public class SerializeField:Attribute{}public class HideInInspector:Attribute{}public class ContextMenu:Attribute{public ContextMenu(string s){}}
}
namespace UnityEngine.Rendering {public enum ShadowCastingMode{Off}public enum LightProbeUsage{Off}public enum ReflectionProbeUsage{Off}}

namespace UnityEngine {
 public struct Ray{public Vector3 origin,direction;public Ray(Vector3 a,Vector3 b){origin=a;direction=b;}public Vector3 GetPoint(float d)=>origin+direction*d;}
 public struct Plane{Vector3 normal,point;public Plane(Vector3 n,Vector3 p){normal=n;point=p;}public bool Raycast(Ray r,out float d){float divisor=Vector3.Dot(normal,r.direction);d=divisor==0?0:Vector3.Dot(normal,point-r.origin)/divisor;return divisor!=0&&d>=0;}}
 public struct Rect{
  public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
  public float xMin=>x;public float yMin=>y;public float xMax=>x+width;public float yMax=>y+height;
  public static bool operator==(Rect a,Rect b)=>a.x==b.x&&a.y==b.y&&a.width==b.width&&a.height==b.height;public static bool operator!=(Rect a,Rect b)=>!(a==b);public override bool Equals(object o)=>o is Rect r&&this==r;public override int GetHashCode()=>x.GetHashCode();
 }

}

namespace UnityEngine {
 public enum KeyCode{A,D,W,S,LeftArrow,RightArrow,UpArrow,DownArrow,Space,R,Backspace,Escape}
 public enum TouchPhase{Began,Moved,Stationary,Ended,Canceled}
 public struct Touch{public int fingerId;public Vector2 position;public TouchPhase phase;}
 public static class Input{
  public static bool multiTouchEnabled,MouseHeld,MouseDown,MouseUp;public static Vector2 mousePosition=new Vector2(600,400);public static Touch[] touches=Array.Empty<Touch>();public static HashSet<KeyCode> held=new HashSet<KeyCode>(),down=new HashSet<KeyCode>();
  public static int touchCount=>touches.Length;public static Touch GetTouch(int i)=>touches[i];public static bool GetMouseButton(int n)=>MouseHeld;public static bool GetMouseButtonDown(int n)=>MouseDown;public static bool GetMouseButtonUp(int n)=>MouseUp;public static bool GetKey(KeyCode k)=>held.Contains(k);public static bool GetKeyDown(KeyCode k)=>down.Contains(k);
  public static void Clear(){MouseHeld=MouseDown=MouseUp=false;held.Clear();down.Clear();touches=Array.Empty<Touch>();}
 }
 public class Texture:Object{}public enum TextureFormat{RGBA32}public enum HideFlags{HideAndDontSave}public enum TextureWrapMode{Clamp}public enum FilterMode{Bilinear}
 public struct Color32{public byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public class Texture2D:Texture{public new string name="white";public int width,height;public Color32[] pixels;public HideFlags hideFlags;public TextureWrapMode wrapMode;public FilterMode filterMode;public static Texture2D whiteTexture=new Texture2D(1,1,TextureFormat.RGBA32,false);public Texture2D(int w,int h,TextureFormat f,bool m){width=w;height=h;pixels=new Color32[w*h];if(w==1&&h==1)pixels[0]=new Color32(255,255,255,255);}public void SetPixels32(Color32[] p){pixels=p;}public void Apply(bool a,bool b){}}
 public enum TextAnchor{MiddleCenter}public enum FontStyle{Normal,Bold}public class GUIStyleState{public Color textColor;}
 public class GUIStyle{public TextAnchor alignment;public FontStyle fontStyle;public int fontSize;public GUIStyleState normal=new GUIStyleState();public GUIStyle(){}public GUIStyle(GUIStyle other){alignment=other.alignment;fontStyle=other.fontStyle;}}
 public class GUISkin{public GUIStyle label=new GUIStyle();}
 public static class GUI{
  public static Color color;public static Matrix4x4 matrix;public static int depth;public static GUISkin skin=new GUISkin();public static List<string> labels=new List<string>();
  public static bool Record;public static List<object> commands=new List<object>();public static Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
  static object Bounds(Rect r)=>new[]{r.x,r.y,r.width,r.height};static float[] Rgba(Color c)=>new[]{c.r,c.g,c.b,c.a};
  public static void DrawTexture(Rect r,Texture t){if(!Record)return;var texture=(Texture2D)t;textures[texture.name]=texture;commands.Add(new{kind="texture",rect=Bounds(r),texture=texture.name,color=Rgba(color)});}
  public static void Label(Rect r,string s,GUIStyle style){labels.Add(s);if(Record)commands.Add(new{kind="text",rect=Bounds(r),text=s,fontSize=style.fontSize,bold=style.fontStyle==FontStyle.Bold,color=Rgba(style.normal.textColor)});}
  public static void Save(string path){var images=new List<object>();foreach(var pair in textures){var t=pair.Value;var bytes=new byte[t.pixels.Length*4];for(int i=0;i<t.pixels.Length;i++){bytes[i*4]=t.pixels[i].r;bytes[i*4+1]=t.pixels[i].g;bytes[i*4+2]=t.pixels[i].b;bytes[i*4+3]=t.pixels[i].a;}images.Add(new{name=pair.Key,width=t.width,height=t.height,rgba=Convert.ToBase64String(bytes)});}System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new{width=Screen.width,height=Screen.height,textures=images,commands=commands}));}
 }

}

namespace UnityEngine {
 public enum EventType{Repaint}public class Event{public static Event current=new Event();public EventType type=EventType.Repaint;}
 public enum ScreenOrientation{LandscapeLeft,Portrait}
 public static class Screen{public static int width=1280,height=720;public static Rect safeArea=new Rect(0,0,1280,720);public static ScreenOrientation orientation;}
 public static class Lifecycle {public static void Call(object target,string name,params object[] args){try{target.GetType().GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(target,args);}catch(System.Reflection.TargetInvocationException e){throw e.InnerException;}}}
}
namespace UnityEngine.Events {public class UnityEvent {private event Action listeners;public void AddListener(Action action){listeners+=action;}public void Invoke(){listeners?.Invoke();}}}
namespace IntercomMvp {
 public class IntercomGroundFeedback:UnityEngine.Behaviour {public bool CanAdjustRunningStride;public bool TryGetLocomotionFacing(out UnityEngine.Vector3 f){f=UnityEngine.Vector3.forward;return false;}public bool TryConstrainStationaryHeading(UnityEngine.Vector3 a,float speed,out UnityEngine.Vector3 b){b=a;return false;}}
 public class IntercomSimpleShooter:UnityEngine.Behaviour {}
 public enum IntercomWeaponPosture{Relaxed,Raising,Combat,Lowering}
 public class IntercomWeaponPose:UnityEngine.Behaviour {public IntercomWeaponPosture Posture; public void ResetPosture(){Posture=IntercomWeaponPosture.Relaxed;}private void OnDisable(){ResetPosture();}}
 public class IntercomWeaponMagazine:UnityEngine.Behaviour {public bool IsReloading;public float ReloadProgress;public int CurrentAmmo=24,Capacity=24;}
}

namespace IntercomMvp {
 public enum IntercomNavResult{Success,Unreachable,Deferred}
 public class IntercomNpcNavigation:UnityEngine.Behaviour{public bool IsReady=true;public void RebuildObstacleCache(){IsReady=true;}public bool TryProject(UnityEngine.Vector3 p,out UnityEngine.Vector3 point){point=p;return true;}public IntercomNavResult TryFindPath(UnityEngine.Vector3 a,UnityEngine.Vector3 b,System.Collections.Generic.List<UnityEngine.Vector3> path){path.Clear();path.Add(b);return IntercomNavResult.Success;}}
 public class IntercomNpcDefinition:UnityEngine.ScriptableObject{public UnityEngine.Material bodyMaterial=new UnityEngine.Material(),detailMaterial=new UnityEngine.Material(),telegraphMaterial=new UnityEngine.Material();public static float Safe(float v,float fallback,float min,float max)=>float.IsNaN(v)||float.IsInfinity(v)?fallback:UnityEngine.Mathf.Clamp(v,min,max);}
 public enum IntercomNpcPhase{Idle,Chase,Windup,Recovery,Dead}
 public class IntercomNpcController:UnityEngine.Behaviour{public IntercomHealth Health;public IntercomHealth targetHealth;public IntercomNpcPhase Phase;public static IntercomNpcController Create(IntercomNpcDefinition d,IntercomNpcNavigation n,IntercomHealth p,IntercomPlayerMotor m,UnityEngine.Vector3 pos,int i){var g=new UnityEngine.GameObject();g.transform.position=pos;var actor=g.AddComponent<IntercomNpcController>();actor.Health=g.AddComponent<IntercomHealth>();return actor;}public void SetTarget(IntercomHealth h){targetHealth=h;}public void PrepareForSpawn(UnityEngine.Vector3 p){transform.position=p;Health.ResetHealth();}public void Investigate(UnityEngine.Vector3 p){}public void BeginEmergence(){}}
 public class IntercomRescueAlly:UnityEngine.Behaviour{public IntercomHealth Health;public bool Recruited,WaitingForPlayer,Suspended;public static IntercomRescueAlly Create(IntercomNpcNavigation n,IntercomHealth h,IntercomNpcDefinition d,IntercomMissionDefinition m,UnityEngine.Vector3 pos){var g=new UnityEngine.GameObject();var a=g.AddComponent<IntercomRescueAlly>();a.Health=g.AddComponent<IntercomHealth>();g.transform.position=pos;return a;}public void ResetForMission(IntercomMissionDefinition m,UnityEngine.Vector3 pos){transform.position=pos;Health.maximumHealth=m.allyHealth;Health.ResetHealth();Recruited=false;}}
}
